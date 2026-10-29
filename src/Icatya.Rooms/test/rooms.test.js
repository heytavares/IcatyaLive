import { test } from 'node:test';
import assert from 'node:assert/strict';
import { Miniflare } from 'miniflare';
import { readFile } from 'node:fs/promises';

test('room routing, reconnection replacement, capacity and stale presence cleanup', { timeout: 130000 }, async () => {
  const mf = new Miniflare({ modules: true, script: await readFile(new URL('../worker.js', import.meta.url), 'utf8'),
    compatibilityDate: '2026-08-01', durableObjects: { ROOMS: { className: 'IcatyaRoom', useSQLite: true } } });
  const clients = [];
  async function connect(room, nick, clientKey) {
    const response = await mf.dispatchFetch('http://localhost/v1/rooms/' + room, { headers: { Upgrade: 'websocket' } });
    assert.equal(response.status, 101);
    const ws = response.webSocket;
    ws.accept();
    const queue = [], waiting = [];
    ws.addEventListener('message', e => {
      const value = e.data === 'pong' ? e.data : JSON.parse(e.data);
      if (waiting.length) waiting.shift()(value); else queue.push(value);
    });
    const next = () => queue.length ? Promise.resolve(queue.shift()) : Promise.race([
      new Promise(resolve => waiting.push(resolve)), new Promise((_, reject) => setTimeout(() => reject(new Error('Message timeout')), 5000).unref())]);
    ws.send(JSON.stringify({ type: 'join', nick, clientKey }));
    const welcome = await next();
    assert.equal(welcome.type, 'welcome');
    const c = { ws, next, id: welcome.self, welcome, send: m => ws.send(JSON.stringify(m)) };
    clients.push(c);
    return c;
  }
  try {
    const room = 'a'.repeat(32);
    const a = await connect(room, 'Ana');
    const b = await connect(room, 'Beto');
    assert.equal((await a.next()).type, 'joined');
    assert.equal(b.welcome.members.length, 2);
    const other = await connect('b'.repeat(32), 'Outra sala');
    assert.equal(other.welcome.members.length, 1);
    a.send({ type: 'signal', to: b.id, from: 'spoofed', kind: 'offer', code: 'MOAB1.test' });
    assert.deepEqual(await b.next(), { type: 'signal', from: a.id, kind: 'offer', code: 'MOAB1.test' });
    b.send({ type: 'sharing', active: true });
    assert.deepEqual(await a.next(), { type: 'sharing', from: b.id, active: true });
    a.ws.close(1000);
    assert.deepEqual(await b.next(), { type: 'left', id: a.id });
    const c = await connect(room, 'Caio');
    assert.equal(c.welcome.members.length, 2);
    assert.equal(c.welcome.members.find(m => m.id === b.id).sharing, true);
    assert.equal((await b.next()).type, 'joined');
    c.ws.send('ping');
    assert.equal(await c.next(), 'pong');
    b.ws.close(1000); c.ws.close(1000);
    await new Promise(resolve => setTimeout(resolve, 50));
    const d = await connect(room, 'Dora');
    assert.equal(d.welcome.members.length, 1);
    // Malformed input closes only the offender, without crashing the room.
    const closed = new Promise(resolve => d.ws.addEventListener('close', resolve));
    d.ws.send('{bad json');
    assert.equal((await closed).code, 1008);
    assert.equal((await mf.dispatchFetch('http://localhost/v1/rooms/invalid')).status, 404);
    const crowded = [];
    for (let i = 0; i < 12; i++) crowded.push(await connect('c'.repeat(32), 'Pessoa ' + i));
    assert.equal(crowded.at(-1).welcome.members.length, 12);
    const full = await mf.dispatchFetch('http://localhost/v1/rooms/' + 'c'.repeat(32), { headers: { Upgrade: 'websocket' } });
    full.webSocket.accept();
    const fullClosed = new Promise(resolve => full.webSocket.addEventListener('close', resolve));
    full.webSocket.send(JSON.stringify({ type: 'join', nick: 'Too many' }));
    assert.equal((await fullClosed).code, 4002);
    const oversized = new Promise(resolve => crowded[0].ws.addEventListener('close', resolve));
    crowded[0].ws.send('x'.repeat(32769));
    assert.equal((await oversized).code, 1009);
    const watcher = await connect('d'.repeat(32), 'Watch');
    const old = await connect('d'.repeat(32), 'kira', '1'.repeat(32));
    await watcher.next();
    const namesake = await connect('d'.repeat(32), 'kira', '2'.repeat(32));
    await watcher.next(); await old.next();
    const replaced = new Promise(resolve => old.ws.addEventListener('close', resolve));
    const returned = await connect('d'.repeat(32), 'kira', '1'.repeat(32));
    assert.equal((await replaced).code, 4001);
    assert.deepEqual(await watcher.next(), { type: 'left', id: old.id });
    assert.equal((await watcher.next()).member.id, returned.id);
    assert.equal(returned.welcome.members.length, 3);
    assert(returned.welcome.members.some(m => m.id === namesake.id));
    assert(!returned.welcome.members.some(m => m.id === old.id));
    assert(returned.welcome.members.every(m => !('clientKey' in m)));
    returned.send({ type: 'heartbeat' });
    assert.equal((await returned.next()).type, 'pong');
    returned.send({ type: 'profile', avatar: 'shark' });
    const changed = await watcher.next();
    assert.equal(changed.type, 'profile');
    assert.equal(changed.member.id, returned.id);
    assert.equal(changed.member.avatar, 'shark');
    assert.equal((await returned.next()).member.avatar, 'shark');
    const snapshot = await connect('d'.repeat(32), 'Snapshot');
    assert.equal(snapshot.welcome.members.find(m => m.id === returned.id).avatar, 'shark');
    console.log('PASS avatar broadcasts and new participant snapshots');
    console.log('PASS same client replaces stale connection; identical nicknames on different clients remain separate');
    const live = await connect('e'.repeat(32), 'Alive');
    const silent = await connect('e'.repeat(32), 'Lost internet');
    await live.next();
    const keepAlive = setInterval(() => live.ws.send('ping'), 20000);
    try {
      await new Promise(resolve => setTimeout(resolve, 91000));
      // Consume automatic pongs; the legacy client stays alive without waking the object.
      for (let i = 0; i < 4; i++) assert.equal(await live.next(), 'pong');
      live.send({ type: 'heartbeat' });
      assert.deepEqual(await live.next(), { type: 'left', id: silent.id });
      assert.equal((await live.next()).type, 'pong');
      console.log('PASS silent half-open presence expires while a healthy client stays online');
    } finally { clearInterval(keepAlive); }
  } finally {
    for (const c of clients) try { c.ws.close(1000); } catch { }
    await mf.dispose();
  }
});
