// index.js (CommonJS, orchestrator – auto-switch Apple <-> YouTube)

const { spawn } = require('child_process');
const readline = require('readline');

const {
  initAppleRpc,
  updateApplePresence,
  clearApplePresence,
} = require('./apple_music');

const {
  initYoutubeRpc,
  updateYoutubePresence,
  clearYoutubePresence,
} = require('./youtube_presence');

/* =======================
   CONFIG
======================= */

const fs = require('fs');
const path = require('path');

// Standalone Detection: Check for pre-compiled helper EXE
const helperExePath = path.join(__dirname, '../NowPlayingHelper/NowPlayingHelper.exe');
const isExePresent = fs.existsSync(helperExePath);

const HELPER_COMMAND = isExePresent ? helperExePath : 'dotnet';
const HELPER_ARGS = isExePresent ? [] : ['run', '--project', path.join(__dirname, '../NowPlayingHelper/NowPlayingHelper.csproj')];

// Default config: enable both if no input received yet
let config = {
  apple: true,
  youtube: true,
  youtubePrivacy: false,
};

let helperStarted = false;
let helperProcess = null; // Store reference

/* =======================
   PLATFORM DETECTION
======================= */

function detectPlatform(payload) {
  const s = String(payload.source || '').toLowerCase();
  const t = String(payload.title || '').toLowerCase();
  const a = String(payload.artist || '').toLowerCase();

  // 1) Spotify check (Desktop or Web)
  if (s.includes('spotify') || t.includes('spotify') || a.includes('spotify')) {
    return 'spotify';
  }

  // 2) Apple Music check
  if (s.includes('apple')) {
    return 'apple';
  }

  // 3) YouTube / Browser check
  const browserEngines = ['chrome', 'edge', 'opera', 'firefox', 'browser', 'youtube'];
  if (browserEngines.some(engine => s.includes(engine))) {
    return 'youtube';
  }

  return 'other';
}

/* =======================
   HELPER PROCESS
======================= */

function startHelper() {
  if (helperStarted) return;
  helperStarted = true;

  console.log('[HELPER] Starting NowPlayingHelper...');
  helperProcess = spawn(HELPER_COMMAND, HELPER_ARGS, {
    stdio: ['pipe', 'pipe', 'pipe'],
  });

  const rl = readline.createInterface({
    input: helperProcess.stdout,
    crlfDelay: Infinity,
  });

  rl.on('line', async (line) => {
    try {
      const payload = JSON.parse(line);
      handlePayload(payload);
    } catch (err) {
      console.error('[HELPER] JSON parse error:', err?.message);
    }
  });

  helperProcess.stderr.on('data', (data) => {
    console.error('[HELPER ERROR]', data.toString());
  });

  helperProcess.on('exit', (code) => {
    console.log(`[HELPER] exited with code ${code}`);
    helperProcess = null;
    helperStarted = false;
    clearApplePresence();
    clearYoutubePresence();
  });
}

function stopHelper() {
  if (helperProcess) {
    console.log('[HELPER] Stopping...');
    helperProcess.kill();
    helperProcess = null;
    helperStarted = false;
  }
}

/* =======================
   PAYLOAD ROUTING
======================= */

async function handlePayload(payload) {
  const { status, source } = payload;

  // Debug için istersen bırak
  console.log('[PAYLOAD]', status, '| source:', source);

  // None / Error durumunda her şeyi temizle
  if (status === 'None' || status === 'Error') {
    await clearApplePresence();
    await clearYoutubePresence();
    return;
  }

  const platform = detectPlatform(payload);

  if (platform === 'spotify') {
    // Spotify: hiçbir şey gösterme
    await clearApplePresence();
    await clearYoutubePresence();
    return;
  }

  if (platform === 'apple') {
    // Apple aktif, YouTube pasif
    // Sadece config.apple true ise göster
    if (config.apple) {
      await updateApplePresence(payload, true);
    } else {
      await clearApplePresence();
    }

    // YouTube kesinlikle temizlenmeli
    await updateYoutubePresence(payload, false);
    return;
  }

  if (platform === 'youtube') {
    // YouTube aktif, Apple pasif
    // Sadece config.youtube true ise göster
    if (config.youtube) {
      // Eğer gizlilik modu açıksa verileri maskele
      let finalPayload = payload;
      if (config.youtubePrivacy) {
        finalPayload = {
          ...payload,
          title: 'Video İzleniyor',
          artist: 'Gizli Mod', // Sanatçı kısmını boşalt, "on YouTube" yazar
          album: ''   // Varsa albümü de temizle
        };
      }
      await updateYoutubePresence(finalPayload, true);
    } else {
      await clearYoutubePresence();
    }

    // Apple kesinlikle temizlenmeli
    await updateApplePresence(payload, false);
    return;
  }

  // Tanınmayan bir platform ise her şeyi temizle
  await clearApplePresence();
  await clearYoutubePresence();
}

/* =======================
   IPC / STDIN CONFIG
======================= */

function setupIpc() {
  const rl = readline.createInterface({
    input: process.stdin,
    output: process.stdout,
    terminal: false
  });

  rl.on('line', (line) => {
    try {
      const newConfig = JSON.parse(line);
      if (typeof newConfig.apple === 'boolean') config.apple = newConfig.apple;
      if (typeof newConfig.youtube === 'boolean') config.youtube = newConfig.youtube;
      if (typeof newConfig.youtubePrivacy === 'boolean') config.youtubePrivacy = newConfig.youtubePrivacy;

      console.log('[CONFIG] Updated:', config);

      if (!config.apple) clearApplePresence();
      if (!config.youtube) clearYoutubePresence();

    } catch (err) { }
  });

  // Self-terminate if stdin (parent pipe) closes
  rl.on('close', () => {
    console.log('[IPC] Stdin closed, exiting...');
    gracefulExit();
  });
}

async function gracefulExit() {
  console.log('[SHUTDOWN] Cleaning up...');
  stopHelper();
  await clearApplePresence();
  await clearYoutubePresence();
  process.exit(0);
}

// Handle signals
process.on('SIGINT', gracefulExit);
process.on('SIGTERM', gracefulExit);

/* =======================
   STARTUP
======================= */

// Önce iki RPC client'ı başlat
initAppleRpc();
initYoutubeRpc();

// IPC ayarla
setupIpc();

// Sonra helper'ı başlat
startHelper();
