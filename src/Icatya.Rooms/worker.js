import { DurableObject } from 'cloudflare:workers';

const MAX_MEMBERS = 12;
const PRESENCE_TIMEOUT = 90000;
const encoder = new TextEncoder();
const validName = name => typeof name === 'string' && name.trim().length > 0 && name.trim().length <= 48 && !/[\p{Cc}\p{Cf}\p{Cs}]/u.test(name);
const avatars = new Set(['bear', 'cat', 'dog', 'dragon', 'gorilla', 'meerkat', 'panda', 'penguin', 'shark', 'tiger']);
const member = a => ({ id: a.id, nick: a.nick, sharing: a.sharing, avatar: avatars.has(a.avatar) ? a.avatar : 'cat' });

export default {
  async fetch(request, env) {
    const url = new URL(request.url);
    if (url.pathname === '/health') return Response.json({ service: 'icatya-rooms', protocol: 1 });
    if (url.pathname === '/v1/rooms/summaries' && request.method === 'POST') {
      if (Number(request.headers.get('Content-Length')) > 4096) return new Response('Too large', { status: 413 });
      if (!request.body) return new Response('Invalid JSON', { status: 400 });
      const reader = request.body.getReader(), chunks = [];
      let length = 0;
      while (true) {
        const part = await reader.read();
        if (part.done) break;
        length += part.value.byteLength;
        if (length > 4096) { await reader.cancel(); return new Response('Too large', { status: 413 }); }
        chunks.push(part.value);
      }
      const bytes = new Uint8Array(length); let offset = 0;
      for (const chunk of chunks) { bytes.set(chunk, offset); offset += chunk.byteLength; }
      const raw = new TextDecoder().decode(bytes);
      let body;
      try { body = JSON.parse(raw); } catch { return new Response('Invalid JSON', { status: 400 }); }
      if (!Array.isArray(body?.codes) || body.codes.length < 1 || body.codes.length > 20 ||
          body.codes.some(code => typeof code !== 'string' || !/^[a-f0-9]{32}$/i.test(code)))
        return new Response('Invalid room list', { status: 400 });
      const codes = [...new Set(body.codes.map(code => code.toLowerCase()))];
      const rooms = await Promise.all(codes.map(async code => {
        const response = await env.ROOMS.get(env.ROOMS.idFromName(code)).fetch('https://room/summary');
        return { code, ...await response.json() };
      }));
      return Response.json({ rooms }, { headers: { 'Cache-Control': 'no-store' } });
    }
    const match = /^\/v1\/rooms\/([a-f0-9]{32})$/.exec(url.pathname);
    if (!match || request.method !== 'GET' || request.headers.get('Upgrade')?.toLowerCase() !== 'websocket')
      return new Response('Not found', { status: 404 });
    // The random room identifier is a bearer invitation. Never log URLs or signaling payloads.
    return env.ROOMS.get(env.ROOMS.idFromName(match[1])).fetch(request);
  }
};

export class IcatyaRoom extends DurableObject {
  constructor(ctx, env) {
    super(ctx, env);
    this.ctx = ctx;
    this.name = 'Sala de amigos';
    this.hasName = false;
    ctx.blockConcurrencyWhile(async () => {
      const saved = await ctx.storage.get('roomName');
      if (validName(saved)) { this.name = saved; this.hasName = true; }
    });
    ctx.setWebSocketAutoResponse(new WebSocketRequestResponsePair('ping', 'pong'));
  }
  sockets() { return this.ctx.getWebSockets().filter(s => s.readyState === WebSocket.OPEN); }
  joined() { return this.sockets().filter(s => s.deserializeAttachment()?.nick); }
  send(socket, value) { try { socket.send(JSON.stringify(value)); } catch { /* close handler removes presence */ } }
  broadcast(value, except) { for (const s of this.joined()) if (s !== except) this.send(s, value); }
  remove(socket, code = 1000, reason = 'Disconnected') {
    const a = socket.deserializeAttachment();
    socket.serializeAttachment(null);
    if (a?.nick) this.broadcast({ type: 'left', id: a.id });
    try { socket.close(code, reason); } catch { }
  }
  prune() {
    for (const s of this.sockets()) {
      const a = s.deserializeAttachment();
      if (!a) continue;
      const last = Math.max(a.seen ?? a.start, this.ctx.getWebSocketAutoResponseTimestamp(s)?.getTime() ?? 0);
      if (Date.now() - last > (a.nick ? PRESENCE_TIMEOUT : 15000)) this.remove(s, 4000, 'Presence expired');
    }
  }
  async fetch(request) {
    this.prune();
    if (new URL(request.url).pathname === '/summary')
      return Response.json({ name: this.name, count: this.joined().length,
        avatars: this.joined().slice(0, 3).map(s => member(s.deserializeAttachment()).avatar) });
    // Reserve pending handshakes so a returning member can replace a stale slot even in a full room.
    if (this.sockets().length >= MAX_MEMBERS + 4) return new Response('Room full', { status: 429 });
    const pair = new WebSocketPair();
    this.ctx.acceptWebSocket(pair[1]);
    pair[1].serializeAttachment({ id: crypto.randomUUID(), nick: null, sharing: false, start: Date.now(), seen: Date.now(), count: 0 });
    return new Response(null, { status: 101, webSocket: pair[0] });
  }
  async webSocketMessage(socket, raw) {
    this.prune();
    if (typeof raw !== 'string' || encoder.encode(raw).length > 32768) { socket.close(1009, 'Message too large'); return; }
    const a = socket.deserializeAttachment();
    if (!a) { socket.close(1008, 'Invalid session'); return; }
    if (Date.now() - a.start >= 10000) { a.start = Date.now(); a.count = 0; }
    if (++a.count > 80) { socket.close(1008, 'Too many messages'); return; }
    a.seen = Date.now();
    socket.serializeAttachment(a);
    let m;
    try { m = JSON.parse(raw); } catch { socket.close(1008, 'Invalid JSON'); return; }
    if (!m || typeof m !== 'object') { socket.close(1008, 'Invalid message'); return; }
    if (m.type === 'join' && !a.nick) {
      const nick = typeof m.nick === 'string' ? m.nick.trim() : '';
      if (!nick || nick.length > 24 || /[\p{Cc}\p{Cf}\p{Cs}]/u.test(nick)) { socket.close(1008, 'Invalid nickname'); return; }
      if (m.avatar !== undefined && !avatars.has(m.avatar)) { socket.close(1008, 'Invalid avatar'); return; }
      if (m.clientKey !== undefined && (typeof m.clientKey !== 'string' || !/^[a-f0-9]{32}$/.test(m.clientKey))) {
        socket.close(1008, 'Invalid client identity'); return;
      }
      if (m.clientKey) {
        for (const old of this.joined())
          if (old !== socket && old.deserializeAttachment().clientKey === m.clientKey)
            this.remove(old, 4001, 'Replaced by a newer connection');
      }
      if (this.joined().length >= MAX_MEMBERS) { this.remove(socket, 4002, 'Room full'); return; }
      if (!this.hasName && validName(m.roomName)) {
        this.name = m.roomName.trim(); this.hasName = true;
        await this.ctx.storage.put('roomName', this.name);
      }
      a.nick = nick;
      a.avatar = m.avatar ?? 'cat';
      a.clientKey = m.clientKey ?? null;
      socket.serializeAttachment(a);
      this.send(socket, { type: 'welcome', self: a.id, room: { name: this.name, canRename: true }, members: this.joined().map(s => member(s.deserializeAttachment())) });
      this.broadcast({ type: 'joined', member: member(a) }, socket);
    } else if (!a.nick) {
      socket.close(1008, 'Join first');
    } else if (m.type === 'profile' && avatars.has(m.avatar)) {
      a.avatar = m.avatar;
      socket.serializeAttachment(a);
      this.broadcast({ type: 'profile', member: member(a) });
    } else if (m.type === 'rename') {
      if (!validName(m.name)) { this.send(socket, { type: 'error', message: 'O nome da sala deve ter entre 1 e 48 caracteres, sem caracteres de controle.' }); return; }
      if (Date.now() - (a.renamedAt ?? 0) < 1500) { this.send(socket, { type: 'error', message: 'Aguarde um instante antes de renomear novamente.' }); return; }
      const name = m.name.trim();
      if (name !== this.name) {
        await this.ctx.storage.put('roomName', name); this.name = name; this.hasName = true;
      }
      a.renamedAt = Date.now(); socket.serializeAttachment(a);
      this.broadcast({ type: 'room', name: this.name });
    } else if (m.type === 'heartbeat') {
      this.send(socket, { type: 'pong' });
    } else if (m.type === 'sharing' && typeof m.active === 'boolean') {
      a.sharing = m.active;
      socket.serializeAttachment(a);
      this.broadcast({ type: 'sharing', from: a.id, active: a.sharing }, socket);
    } else if (m.type === 'signal' && ['offer', 'answer'].includes(m.kind) &&
               typeof m.code === 'string' && m.code.startsWith('MOAB1.') && m.code.length <= 24000) {
      const target = this.joined().find(s => s !== socket && s.deserializeAttachment().id === m.to);
      if (target) this.send(target, { type: 'signal', from: a.id, kind: m.kind, code: m.code });
    } else { socket.close(1008, 'Unsupported message'); }
  }
  webSocketClose(socket, code) {
    this.remove(socket, [1005, 1006, 1015].includes(code) ? 1000 : code);
  }
  webSocketError(socket) { this.webSocketClose(socket, 1011); }
}
