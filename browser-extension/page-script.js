(() => {
  let last = '';
  const publish = () => {
    const media = [...document.querySelectorAll('audio,video')].find(item => !item.paused) || document.querySelector('audio,video');
    const metadata = navigator.mediaSession?.metadata;
    if (!media && !metadata) return;
    const payload = {
      title: metadata?.title || document.title || 'Browser media',
      artist: metadata?.artist || '',
      album: metadata?.album || '',
      sourceApplication: location.hostname,
      sourceId: `browser:${location.hostname}`,
      isPlaying: media ? !media.paused : navigator.mediaSession?.playbackState === 'playing',
      positionSeconds: media && Number.isFinite(media.currentTime) ? media.currentTime : 0,
      durationSeconds: media && Number.isFinite(media.duration) ? media.duration : 0,
      artworkDataUrl: null
    };
    const serialized = JSON.stringify(payload);
    if (serialized === last) return;
    last = serialized;
    window.postMessage({ channel: 'nowspinning-media-v1', payload }, location.origin);
  };
  document.addEventListener('play', publish, true);
  document.addEventListener('pause', publish, true);
  document.addEventListener('durationchange', publish, true);
  setInterval(publish, 2000);
})();
