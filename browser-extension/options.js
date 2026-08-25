const input = document.querySelector('#token');
const status = document.querySelector('#status');
chrome.storage.local.get('token').then(({ token }) => { input.value = token || ''; });
document.querySelector('#save').addEventListener('click', async () => {
  const token = input.value.trim().toLowerCase();
  if (!/^[a-f0-9]{64}$/.test(token)) { status.textContent = 'Token must contain exactly 64 hexadecimal characters.'; return; }
  await chrome.storage.local.set({ token });
  status.textContent = 'Saved. Media metadata will use the authenticated local connection.';
});
