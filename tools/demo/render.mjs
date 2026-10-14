// Renders tools/demo/demo.html frame by frame in headless Chrome/Edge (DevTools protocol, no npm packages)
// and encodes docs/assets/demo.gif with ffmpeg.
// Usage: node tools/demo/render.mjs [--fps 12] [--frames t1,t2,...]   (--frames: only PNG stills, for review)
// Needs: Node 22+, Chrome or Edge, ffmpeg on PATH (or FFMPEG=path).
import { spawn, spawnSync } from 'node:child_process';
import { mkdtempSync, mkdirSync, rmSync, writeFileSync, existsSync, statSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join, resolve, dirname } from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';

const here = dirname(fileURLToPath(import.meta.url));
const root = resolve(here, '../..');
const args = Object.fromEntries(process.argv.slice(2).map((a, i, all) => a.startsWith('--') ? [a.slice(2), all[i + 1]] : null).filter(Boolean));
const fps = Number(args.fps ?? 12);
const W = 960, H = 600;

const browsers = [
  process.env.CHROME,
  'C:/Program Files/Google/Chrome/Application/chrome.exe',
  'C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe',
  '/usr/bin/google-chrome', '/usr/bin/chromium',
].filter(Boolean);
const browser = browsers.find(existsSync);
if (!browser) throw new Error('Chrome or Edge not found; set CHROME=path');

const profile = mkdtempSync(join(tmpdir(), 'tt-demo-'));
const port = 9300 + Math.floor(Math.random() * 500);
const proc = spawn(browser, ['--headless=new', `--remote-debugging-port=${port}`, `--user-data-dir=${profile}`,
  '--hide-scrollbars', '--force-device-scale-factor=1', `--window-size=${W},${H}`, 'about:blank'], { stdio: 'ignore' });

async function target() {
  for (let i = 0; i < 50; i++) {
    try {
      const list = await (await fetch(`http://127.0.0.1:${port}/json/list`)).json();
      const page = list.find(t => t.type === 'page');
      if (page) return page.webSocketDebuggerUrl;
    } catch { /* not up yet */ }
    await new Promise(r => setTimeout(r, 200));
  }
  throw new Error('browser did not start');
}

const ws = new WebSocket(await target());
await new Promise(r => ws.addEventListener('open', r, { once: true }));
let seq = 0;
const pending = new Map();
ws.addEventListener('message', e => {
  const m = JSON.parse(e.data);
  if (m.id && pending.has(m.id)) { pending.get(m.id)(m); pending.delete(m.id); }
});
const cdp = (method, params = {}) => new Promise((ok, fail) => {
  const id = ++seq;
  pending.set(id, m => m.error ? fail(new Error(`${method}: ${m.error.message}`)) : ok(m.result));
  ws.send(JSON.stringify({ id, method, params }));
});
const evaluate = async expr => (await cdp('Runtime.evaluate', { expression: expr, awaitPromise: true, returnByValue: true })).result.value;

try {
  await cdp('Emulation.setDeviceMetricsOverride', { width: W, height: H, deviceScaleFactor: 1, mobile: false });
  await cdp('Page.navigate', { url: pathToFileURL(join(here, 'demo.html')).href + '#render' });
  for (let i = 0; i < 50 && !(await evaluate('typeof window.renderAt === "function" && document.fonts.status === "loaded"')); i++)
    await new Promise(r => setTimeout(r, 100));
  const total = await evaluate('window.TOTAL');

  const shot = async (t, file) => {
    await evaluate(`window.renderAt(${t})`);
    const { data } = await cdp('Page.captureScreenshot', { format: 'png', clip: { x: 0, y: 0, width: W, height: H, scale: 1 } });
    writeFileSync(file, Buffer.from(data, 'base64'));
  };

  if (args.frames) {
    const out = join(root, 'tools/demo/out');
    mkdirSync(out, { recursive: true });
    for (const t of args.frames.split(',')) await shot(Number(t), join(out, `still-${t}.png`));
    console.log(`stills in ${out}`);
  } else {
    const dir = mkdtempSync(join(tmpdir(), 'tt-frames-'));
    const n = Math.round(total * fps);
    for (let i = 0; i < n; i++) await shot(i / fps, join(dir, `f${String(i).padStart(4, '0')}.png`));
    const gif = join(root, 'docs/assets/demo.gif');
    const ffmpeg = process.env.FFMPEG ?? 'ffmpeg';
    const r = spawnSync(ffmpeg, ['-y', '-loglevel', 'error', '-framerate', String(fps), '-i', join(dir, 'f%04d.png'),
      '-filter_complex', '[0:v]split[a][b];[a]palettegen=max_colors=128:stats_mode=diff[p];[b][p]paletteuse=dither=none:diff_mode=rectangle',
      '-loop', '0', gif], { stdio: 'inherit' });
    rmSync(dir, { recursive: true, force: true });
    if (r.status !== 0) throw new Error('ffmpeg failed');
    console.log(`${gif}: ${n} frames, ${total.toFixed(1)} s, ${(statSync(gif).size / 1024).toFixed(0)} KB`);
  }
} finally {
  ws.close();
  const exited = new Promise(r => proc.once('exit', r));
  proc.kill();
  await exited;
  try { rmSync(profile, { recursive: true, force: true, maxRetries: 10, retryDelay: 200 }); } catch { /* temp profile, the OS cleans it */ }
}
