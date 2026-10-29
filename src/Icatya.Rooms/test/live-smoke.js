// Explicit smoke check against the configured deployment. Uses only a new, random test room.
// Never prints invitations, real room data or account credentials; never transmits media.
import assert from 'node:assert/strict';
import { randomBytes } from 'node:crypto';

if (!process.env.ICATYA_LIVE_TEST_URL) throw new Error('Set ICATYA_LIVE_TEST_URL explicitly.');
const base = new URL(process.env.ICATYA_LIVE_TEST_URL);
if (base.protocol !== 'https:' || base.pathname !== '/') throw new Error('Expected HTTPS service root.');
const code = randomBytes(16).toString('hex');
const socket = new WebSocket(new URL(`/v1/rooms/${code}`, base).href.replace('https:', 'wss:'));
const messages = [], waiting = [];
socket.addEventListener('message', event => {
  const value = JSON.parse(event.data);
  if (waiting.length) waiting.shift()(value); else messages.push(value);
});
function timeout(promise) {
  return Promise.race([promise, new Promise((_, reject) => setTimeout(() => reject(new Error('Smoke timeout')), 15000).unref())]);
}
const next = () => messages.length ? Promise.resolve(messages.shift()) : timeout(new Promise(resolve => waiting.push(resolve)));
async function summary() {
  const response = await fetch(new URL('v1/rooms/summaries', base), { method: 'POST',
    headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ codes: [code] }) });
  assert.equal(response.status, 200); return (await response.json()).rooms[0];
}
try {
  await timeout(new Promise((resolve, reject) => { socket.addEventListener('open', resolve, { once: true }); socket.addEventListener('error', reject, { once: true }); }));
  socket.send(JSON.stringify({ type: 'join', nick: 'Verificação', roomName: 'Verificação iCatya' }));
  const welcome = await next();
  assert.equal(welcome.type, 'welcome'); assert.equal(welcome.room.canRename, true);
  socket.send(JSON.stringify({ type: 'rename', name: 'Verificação de salas' }));
  assert.deepEqual(await next(), { type: 'room', name: 'Verificação de salas' });
  const online = await summary(); assert.equal(online.name, 'Verificação de salas'); assert.equal(online.count, 1);
  const closed = new Promise(resolve => socket.addEventListener('close', resolve, { once: true }));
  socket.close(1000); await timeout(closed);
  const empty = await summary(); assert.equal(empty.count, 0); assert.equal(empty.name, 'Verificação de salas');
  console.log('PASS deployed service: compatible join, shared rename, real count and saved name after leaving; no media/real-room access.');
} finally { if (socket.readyState < 2) socket.close(1000); }
