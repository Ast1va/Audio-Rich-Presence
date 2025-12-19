// youtube_presence.js
const RPC = require('discord-rpc');

const DISCORD_YT_CLIENT_ID = '1449855991599599778';

RPC.register(DISCORD_YT_CLIENT_ID);
const rpc = new RPC.Client({ transport: 'ipc' });

let ready = false;
let lastActivityKey = null;

function initYoutubeRpc() {
  rpc.on('ready', () => {
    ready = true;
    console.log('Discord RPC (YouTube) connected');
  });

  rpc
    .login({ clientId: DISCORD_YT_CLIENT_ID })
    .catch((err) => console.error('Failed to connect to Discord RPC (YouTube):', err));
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

async function clearYoutubePresence() {
  if (!ready) return;
  try {
    await rpc.clearActivity();
  } catch {
    // ignore
  }
  lastActivityKey = null;
}

async function updateYoutubePresence(payload, isActive) {
  if (!ready) {
    return;
  }

  if (!isActive) {
    await clearYoutubePresence();
    return;
  }

  const {
    status,
    title: rawTitle,
    artist: rawArtist,
    positionSeconds,
    durationSeconds,
  } = payload;

  const title = rawTitle || 'Unknown Video';
  const artist = rawArtist || '';

  if (status === 'None' || status === 'Error') {
    await clearYoutubePresence();
    return;
  }

  const activityKey = `${status}|${title}|${artist}|youtube`;
  if (activityKey === lastActivityKey) {
    return;
  }
  lastActivityKey = activityKey;

  const activity = {
    details: status === 'Paused' ? '⏸ Paused' : `▶️ ${title}`,
    state:
      status === 'Paused'
        ? title
        : artist
        ? `by ${artist}`
        : 'on YouTube',
    largeImageKey: 'youtube',
    largeImageText: 'YouTube',
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
    console.error('[YouTube] Failed to set activity:', err?.message);
  }
}

module.exports = {
  initYoutubeRpc,
  updateYoutubePresence,
  clearYoutubePresence,
};
