const script = document.createElement('script');
script.src = chrome.runtime.getURL('page-script.js');
script.onload = () => script.remove();
(document.head || document.documentElement).appendChild(script);

window.addEventListener('message', (event) => {
  if (event.source !== window || event.data?.channel !== 'nowspinning-media-v1') return;
  chrome.runtime.sendMessage({ type: 'media', payload: event.data.payload });
});
