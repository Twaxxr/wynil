const endpoint = 'ws://127.0.0.1:17842/nowspinning/';
let socket;
let queued;

async function connect() {
  if (socket?.readyState === WebSocket.OPEN || socket?.readyState === WebSocket.CONNECTING) return;
  const { token } = await chrome.storage.local.get('token');
  if (!token || !/^[a-f0-9]{64}$/.test(token)) return;
  socket = new WebSocket(`${endpoint}?token=${encodeURIComponent(token)}`);
  socket.onopen = () => { if (queued) { socket.send(queued); queued = undefined; } };
  socket.onclose = () => { socket = undefined; };
  socket.onerror = () => socket?.close();
}

chrome.runtime.onMessage.addListener((message) => {
  if (message?.type !== 'media') return;
  const serialized = JSON.stringify(message.payload);
  if (serialized.length > 6 * 1024 * 1024) return;
  if (socket?.readyState === WebSocket.OPEN) socket.send(serialized);
  else { queued = serialized; void connect(); }
});

chrome.storage.onChanged.addListener(() => { socket?.close(); void connect(); });
