import { Miniflare } from 'miniflare';
import { readFile } from 'node:fs/promises';
let script = await readFile(new URL('../worker.js', import.meta.url), 'utf8');
// Fault injection stays in this local fixture; the deployed worker is never changed.
if (process.env.ICATYA_TEST_DROP_FIRST_OFFER === '1') {
  const target = "if (target) this.send(target, { type: 'signal', from: a.id, kind: m.kind, code: m.code });";
  if (!script.includes(target)) throw new Error('Signaling fixture changed; cannot inject dropped offer.');
  script = script.replace(target, `if (m.kind === 'offer' && !a.testDropped) {
    a.testDropped = true; socket.serializeAttachment(a); return;
  }
  ${target}`);
  console.log('FAULT TEST: dropping the first outgoing offer per participant.');
}
const mf = new Miniflare({ modules: true, script,
  host: '127.0.0.1', port: 8787, compatibilityDate: '2026-08-01',
  durableObjects: { ROOMS: { className: 'IcatyaRoom', useSQLite: true } } });
console.log('Local rooms server: ' + await mf.ready);
process.on('SIGINT', async () => { await mf.dispose(); process.exit(0); });
