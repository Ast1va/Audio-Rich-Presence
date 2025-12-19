// apple_music.js
const RPC = require('discord-rpc');

const DISCORD_APPLE_CLIENT_ID = '1449149656876716042';

RPC.register(DISCORD_APPLE_CLIENT_ID);
const rpc = new RPC.Client({ transport: 'ipc' });

let ready = false;
let lastActivityKey = null;

// RPC client'ı başlat
function initAppleRpc() {
  rpc.on('ready', () => {
    ready = true;
    console.log('Discord RPC (Apple) connected');
  });

  rpc
    .login({ clientId: DISCORD_APPLE_CLIENT_ID })
    .catch((err) => console.error('Failed to connect to Discord RPC (Apple):', err));
}

function buildTimestamps(position, duration) {
  const pos = Number(position);
  const dur = Number(duration);

  if (!Number.isFinite(pos) || !Number.isFinite(dur) || dur <= 0 || pos < 0) {
    return null;
  }

  const now = Math.floor(Date.now() / 1000);
  const start = Math.floor(now - pos);
  const end = Math.floor(start + dur);

  return { startTimestamp: start, endTimestamp: end };
}

// Apple presence'ı tamamen temizle
async function clearApplePresence() {
  if (!ready) return;
  try {
    await rpc.clearActivity();
  } catch {
    // ignore
  }
  lastActivityKey = null;
}

// Apple için presence güncelle
async function updateApplePresence(payload, isActive) {
  if (!ready) {
    // client daha hazır değilken gelirsek sessizce geç
    return;
  }

  // isActive = false ise sadece temizleyip çık
  if (!isActive) {
    await clearApplePresence();
    return;
  }

  const {
    status,
    title: rawTitle,
    artist: rawArtist,
    positionSeconds,
    durationSeconds,
  } = payload;

  const title = rawTitle || 'Unknown Track';
  const artist = rawArtist || 'Unknown Artist';

  if (status === 'None' || status === 'Error') {
    await clearApplePresence();
    return;
  }

  const activityKey = `${status}|${title}|${artist}|apple`;
  if (activityKey === lastActivityKey) {
    return; // spam önleme
  }
  lastActivityKey = activityKey;

  const activity = {
    details: status === 'Paused' ? '⏸ Paused' : `🎧 ${title}`,
    state:
      status === 'Paused'
        ? `${title} — ${artist}`
        : artist
        ? `by ${artist}`
        : 'Listening',
    largeImageKey: 'apple_music',
    largeImageText: 'Apple Music',
  };

  if (status === 'Playing') {
    const ts = buildTimestamps(positionSeconds, durationSeconds);
    if (ts) {
      activity.startTimestamp = ts.startTimestamp;
      activity.endTimestamp = ts.endTimestamp;
    }
  }

  try {
    await rpc.setActivity(activity);
  } catch (err) {
    console.error('[Apple] Failed to set activity:', err?.message);
  }
}

module.exports = {
  initAppleRpc,
  updateApplePresence,
  clearApplePresence,
};
