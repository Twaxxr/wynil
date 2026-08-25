import './style.css';

type TrackPayload = { identity: string; title: string; artist: string; album: string; sourceApplication: string; artworkUrl: string | null };
type SettingsPayload = {
  deskMaterial: string; albumSleeveStyle: string; filmGrainEnabled: boolean; dustParticlesEnabled: boolean;
  mouseParallaxEnabled: boolean; artworkAmbientLightingEnabled: boolean; showSongInformation: boolean;
  filmGrainIntensity: number; dustIntensity: number; parallaxStrength: number; ambientLightingIntensity: number;
  vinylSpeed: number; transitionSeconds: number; tonearmAnimation: boolean; reduceMotion: boolean;
  targetFps: number; lowPowerMode: boolean;
};
type NativeMessage = { version: 1; type: string; requestId?: string; payload?: Record<string, unknown> };

declare global {
  interface Window { chrome?: { webview?: { postMessage(message: unknown): void; addEventListener(type: 'message', listener: (event: MessageEvent) => void): void } } }
}

const scene = document.querySelector<HTMLElement>('#scene')!;
const title = document.querySelector<HTMLElement>('#title')!;
const artist = document.querySelector<HTMLElement>('#artist')!;
const album = document.querySelector<HTMLElement>('#album')!;
const source = document.querySelector<HTMLElement>('#source')!;
const cover = document.querySelector<HTMLElement>('#cover')!;
const label = document.querySelector<HTMLElement>('#label')!;
const dust = document.querySelector<HTMLElement>('#dust')!;
let trackIdentity = '';
let currentArtwork: string | null = null;
let isPlaying = false;
let mouseParallaxEnabled = true;
let parallaxStrength = .25;
let runtimePaused = false;
let positionSeconds = 0;
let durationSeconds = 0;
let timelineAt = performance.now();
const paletteCache = new Map<string, string>();

function applyPointer(x: number, y: number): void {
  if (!mouseParallaxEnabled || runtimePaused || scene.classList.contains('reduce-motion')) return;
  scene.style.setProperty('--parallax-x', `${Math.max(-1, Math.min(1, x)) * 28 * parallaxStrength}px`);
  scene.style.setProperty('--parallax-y', `${Math.max(-1, Math.min(1, y)) * 20 * parallaxStrength}px`);
  scene.style.setProperty('--parallax-fg-x', `${Math.max(-1, Math.min(1, x)) * -8 * parallaxStrength}px`);
  scene.style.setProperty('--parallax-fg-y', `${Math.max(-1, Math.min(1, y)) * -5 * parallaxStrength}px`);
}

function normalized(value: string): string { return value.replace(/([a-z])([A-Z])/g, '$1-$2').replaceAll(' ', '-').toLowerCase(); }

function setArtwork(url: string | null, identity: string): void {
  if (url === currentArtwork) return;
  if (!url) {
    currentArtwork = null;
    cover.classList.add('fallback'); label.classList.add('fallback');
    cover.style.removeProperty('background-image'); label.style.removeProperty('background-image');
    return;
  }
  const preload = new Image();
  preload.draggable = false;
  preload.onload = () => {
    const safeUrl = url.replaceAll('"', '%22');
    scene.classList.add('artwork-changing');
    cover.style.backgroundImage = `url("${safeUrl}")`; label.style.backgroundImage = `url("${safeUrl}")`;
    cover.classList.remove('fallback'); label.classList.remove('fallback');
    currentArtwork = url;
    window.setTimeout(() => scene.classList.remove('artwork-changing'), 360);
    applyPalette(preload, identity);
  };
  preload.src = url;
}

function applyPalette(image: HTMLImageElement, identity: string): void {
  let color = paletteCache.get(identity);
  if (!color) {
    try {
      const canvas = document.createElement('canvas'); canvas.width = canvas.height = 1;
      const context = canvas.getContext('2d', { willReadFrequently: true })!;
      context.drawImage(image, 0, 0, 1, 1);
      const pixel = context.getImageData(0, 0, 1, 1).data;
      color = `${pixel[0]}, ${pixel[1]}, ${pixel[2]}`;
    } catch { color = '217, 145, 79'; }
    paletteCache.set(identity, color);
  }
  scene.style.setProperty('--ambient-rgb', color);
}

function applyTrack(track: TrackPayload): void {
  if (track.identity === trackIdentity) return;
  trackIdentity = track.identity;
  scene.classList.add('track-changing');
  title.textContent = track.title || 'Play something to begin';
  artist.textContent = track.artist || 'Your music will appear here';
  album.textContent = track.album || '';
  source.textContent = track.sourceApplication || 'NOWSPINNING';
  title.classList.toggle('long', title.textContent.length > 34);
  setArtwork(track.artworkUrl, track.identity);
  scene.classList.toggle('idle', !track.title || track.title === 'Play something to begin');
  window.setTimeout(() => scene.classList.remove('track-changing'), 420);
}

function applySettings(settings: SettingsPayload): void {
  scene.dataset.desk = normalized(settings.deskMaterial);
  scene.dataset.sleeve = normalized(settings.albumSleeveStyle);
  scene.classList.toggle('grain-enabled', settings.filmGrainEnabled);
  scene.classList.toggle('dust-enabled', settings.dustParticlesEnabled);
  scene.classList.toggle('ambient-enabled', settings.artworkAmbientLightingEnabled);
  scene.classList.toggle('show-info', settings.showSongInformation);
  scene.classList.toggle('reduce-motion', settings.reduceMotion);
  scene.classList.toggle('tonearm-enabled', settings.tonearmAnimation);
  scene.classList.toggle('low-power', settings.lowPowerMode);
  mouseParallaxEnabled = settings.mouseParallaxEnabled;
  parallaxStrength = settings.parallaxStrength;
  scene.style.setProperty('--grain-opacity', String(settings.filmGrainIntensity * .45));
  scene.style.setProperty('--dust-opacity', String(settings.dustIntensity));
  scene.style.setProperty('--ambient-opacity', String(settings.ambientLightingIntensity));
  scene.style.setProperty('--vinyl-duration', `${1.8 / Math.max(.25, settings.vinylSpeed)}s`);
  scene.style.setProperty('--transition-duration', `${settings.transitionSeconds}s`);
  if (!mouseParallaxEnabled) {
    scene.style.setProperty('--parallax-x', '0px'); scene.style.setProperty('--parallax-y', '0px');
    scene.style.setProperty('--parallax-fg-x', '0px'); scene.style.setProperty('--parallax-fg-y', '0px');
  }
}

function acceptMessage(message: NativeMessage): void {
  if (!message || message.version !== 1) return;
  if (message.type === 'media.track') applyTrack(message.payload as unknown as TrackPayload);
  else if (message.type === 'media.playback') {
    isPlaying = Boolean(message.payload?.isPlaying); timelineAt = performance.now();
    scene.classList.toggle('playing', isPlaying);
  } else if (message.type === 'media.timeline') {
    positionSeconds = Number(message.payload?.positionSeconds ?? 0);
    durationSeconds = Number(message.payload?.durationSeconds ?? 0);
    timelineAt = performance.now();
  } else if (message.type === 'settings.update') {
    try {
      applySettings(message.payload as unknown as SettingsPayload);
      window.chrome?.webview?.postMessage({ version: 1, type: 'settings.applied', requestId: message.requestId, success: true });
    } catch {
      window.chrome?.webview?.postMessage({ version: 1, type: 'settings.applied', requestId: message.requestId, success: false });
    }
  } else if (message.type === 'interaction.update') scene.classList.toggle('interactive', Boolean(message.payload?.enabled));
  else if (message.type === 'runtime.pause') {
    runtimePaused = Boolean(message.payload?.paused);
    scene.classList.toggle('runtime-paused', runtimePaused);
  } else if (message.type === 'pointer.update') {
    applyPointer(Number(message.payload?.x ?? 0), Number(message.payload?.y ?? 0));
  }
}

window.chrome?.webview?.addEventListener('message', (event: MessageEvent<NativeMessage>) => acceptMessage(event.data));

if (!window.chrome?.webview) {
  const token = new URLSearchParams(location.search).get('token');
  if (token && /^[a-f0-9]{64}$/.test(token)) {
    const socket = new WebSocket(`ws://127.0.0.1:17842/nowspinning/?role=viewer&token=${encodeURIComponent(token)}`);
    socket.onmessage = (event) => { try { acceptMessage(JSON.parse(String(event.data)) as NativeMessage); } catch { /* local payload rejected */ } };
  }
}

document.querySelectorAll<HTMLButtonElement>('[data-command]').forEach((button) => button.addEventListener('click', () =>
  window.chrome?.webview?.postMessage({ version: 1, type: 'command', command: button.dataset.command })));

window.addEventListener('mousemove', (event) => {
  const x = (event.clientX / window.innerWidth - .5) * 2;
  const y = (event.clientY / window.innerHeight - .5) * 2;
  applyPointer(x, y);
}, { passive: true });

for (let index = 0; index < 24; index++) {
  const particle = document.createElement('i');
  particle.style.setProperty('--x', `${(index * 43) % 100}%`);
  particle.style.setProperty('--y', `${(index * 71) % 100}%`);
  particle.style.setProperty('--delay', `${-(index % 9)}s`);
  dust.append(particle);
}

function updateTimeline(now: number): void {
  if (runtimePaused) {
    window.setTimeout(() => requestAnimationFrame(updateTimeline), 250);
    return;
  }
  const value = Math.min(durationSeconds, positionSeconds + (isPlaying ? (now - timelineAt) / 1000 : 0));
  scene.style.setProperty('--progress', durationSeconds > 0 ? String(value / durationSeconds) : '0');
  requestAnimationFrame(updateTimeline);
}
requestAnimationFrame(updateTimeline);

(['selectstart', 'dragstart', 'contextmenu', 'drop', 'dragover'] as const).forEach((name) =>
  document.addEventListener(name, (event) => event.preventDefault()));
document.addEventListener('wheel', (event) => { if (event.ctrlKey) event.preventDefault(); }, { passive: false });
document.addEventListener('keydown', (event) => { if (event.key === 'Tab' || (event.ctrlKey && ['+', '-', '0'].includes(event.key))) event.preventDefault(); });

window.chrome?.webview?.postMessage({ version: 1, type: 'ready' });
