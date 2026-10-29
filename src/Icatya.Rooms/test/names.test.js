import { test } from 'node:test';
import assert from 'node:assert/strict';
import { Miniflare } from 'miniflare';
import { readFile } from 'node:fs/promises';

test('shared persistent room names and read-only summaries', { timeout: 30000 }, async () => {
  const script = await readFile(new URL('../worker.js', import.meta.url), 'utf8');
  const options = { modules: true, script, compatibilityDate: '2026-08-01', durableObjects: { ROOMS: { className: 'IcatyaRoom', useSQLite: true } } };
  const mf = new Miniflare(options);
  const clients = [];
  async function connect(code, nick, roomName) {
    const response = await mf.dispatchFetch(`http://localhost/v1/rooms/${code}`, { headers: { Upgrade: 'websocket' } });
    const ws = response.webSocket; ws.accept();
    const messages = [], waiters = [];
    ws.addEventListener('message', event => {
      const value = JSON.parse(event.data);
      if (waiters.length) waiters.shift()(value); else messages.push(value);
    });
    const next = () => messages.length ? Promise.resolve(messages.shift()) : Promise.race([
      new Promise(resolve => waiters.push(resolve)), new Promise((_, reject) => setTimeout(() => reject(new Error('Missing message')), 4000).unref())]);
    ws.send(JSON.stringify({ type: 'join', nick, roomName }));
    const welcome = await next();
    const client = { ws, next, welcome, send: value => ws.send(JSON.stringify(value)) };
    clients.push(client); return client;
  }
  const summaries = codes => mf.dispatchFetch('http://localhost/v1/rooms/summaries', {
    method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ codes }) });
  try {
    const code = 'f'.repeat(32), absent = '0'.repeat(32);
    const a = await connect(code, 'Ana', 'Jogos 🎮');
    assert.equal(a.welcome.room.name, 'Jogos 🎮');
    assert.equal(a.welcome.room.canRename, true);
    const b = await connect(code, 'Beto', 'Old local name');
    assert.equal(b.welcome.room.name, 'Jogos 🎮');
    await a.next();
    a.send({ type: 'rename', name: '  Cinema com amigos  ' });
    assert.deepEqual(await a.next(), { type: 'room', name: 'Cinema com amigos' });
    assert.deepEqual(await b.next(), { type: 'room', name: 'Cinema com amigos' });
    for (const name of ['', 'x'.repeat(49), 'bad\nname', '\ud800']) {
      a.send({ type: 'rename', name }); assert.equal((await a.next()).type, 'error');
    }
    const response = await summaries([code.toUpperCase(), absent, code]);
    assert.equal(response.status, 200);
    const data = await response.json();
    assert.equal(data.rooms.length, 2);
    assert.equal(data.rooms.find(room => room.code === code).name, 'Cinema com amigos');
    assert.equal(data.rooms.find(room => room.code === code).count, 2);
    assert.deepEqual(data.rooms.find(room => room.code === code).avatars, ['cat', 'cat']);
    assert.equal(data.rooms.find(room => room.code === absent).count, 0);
    assert(!JSON.stringify(data).includes('Ana') && !JSON.stringify(data).includes('clientKey'));
    const c = await connect(code, 'Caio');
    assert.equal(c.welcome.members.length, 3, 'Summary lookup must not create ghost participants');
    await a.next(); await b.next();
    a.ws.close(1000); await b.next(); await c.next();
    b.ws.close(1000); await c.next(); c.ws.close(1000);
    await new Promise(resolve => setTimeout(resolve, 50));
    const empty = (await (await summaries([code])).json()).rooms[0];
    assert.equal(empty.count, 0); assert.equal(empty.name, 'Cinema com amigos');
    const returned = await connect(code, 'Retorno', 'Do not overwrite shared name');
    assert.equal(returned.welcome.room.name, 'Cinema com amigos');
    assert.equal(returned.welcome.members.length, 1);
    returned.ws.close(1000); await new Promise(resolve => setTimeout(resolve, 30));
    await mf.setOptions({ ...options, script: script + '\n// Restart object runtime to verify stored room metadata.\n' });
    const cold = await connect(code, 'Nova sessão', 'Do not replace stored name');
    assert.equal(cold.welcome.room.name, 'Cinema com amigos');
    assert.equal(cold.welcome.members.length, 1);
    assert.equal((await summaries(['not-a-room'])).status, 400);
    assert.equal((await summaries(Array(21).fill(code))).status, 400);
    assert.equal((await mf.dispatchFetch('http://localhost/v1/rooms/summaries', { method: 'POST', body: 'x'.repeat(5000) })).status, 413);
    assert.equal((await mf.dispatchFetch(`http://localhost/v1/rooms/${code}/summary`)).status, 404);
    console.log('PASS shared rename, Unicode validation, empty-room persistence, legacy join and real counts without ghost presence');
  } finally {
    for (const client of clients) try { client.ws.close(1000); } catch {}
    await mf.dispose();
  }
});
