// Renders tools/icons/icons.html in headless Chrome/Edge and writes the PNGs into src/TextTools.Plugin/Icons/
// (embedded into the DLL). Usage: node tools/icons/make-icons.mjs. Needs Node 22+, Chrome or Edge.
import { spawnSync } from 'node:child_process';
import { existsSync, mkdirSync, writeFileSync } from 'node:fs';
import { join, resolve, dirname } from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';

const here = dirname(fileURLToPath(import.meta.url));
const out = resolve(here, '../../src/TextTools.Plugin/Icons');
const browser = [process.env.CHROME, 'C:/Program Files/Google/Chrome/Application/chrome.exe',
  'C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe', '/usr/bin/google-chrome', '/usr/bin/chromium']
  .filter(Boolean).find(existsSync);
if (!browser) throw new Error('Chrome or Edge not found; set CHROME=path');

const r = spawnSync(browser, ['--headless=new', '--dump-dom', '--virtual-time-budget=5000',
  pathToFileURL(join(here, 'icons.html')).href], { encoding: 'utf8', maxBuffer: 16 << 20 });
const json = /<pre id="pngs" hidden="">([^<]*)<\/pre>/.exec(r.stdout)?.[1];
if (!json) throw new Error('no icons in the rendered page:\n' + r.stderr);

mkdirSync(out, { recursive: true });
for (const [name, url] of Object.entries(JSON.parse(json.replaceAll('&quot;', '"')))) {
  const png = Buffer.from(url.slice(url.indexOf(',') + 1), 'base64');
  writeFileSync(join(out, `${name}.png`), png);
  console.log(`${name}.png ${png.length} B`);
}
