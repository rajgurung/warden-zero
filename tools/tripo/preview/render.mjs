// usage: node render.mjs <model.glb|model.fbx> <outdir> <shots.json>
// shots.json entries:
//   { "name": "front", "azim": 90, "elev": 8 }                         single still
//   { "name": "run", "sheet": 1, "clip": 3, "azim": 180, "elev": 8 }   contact sheet of a clip
//   { "track": "mixamorigHips", "clip": 3 }                            print bone world positions
import http from 'node:http';
import { readFile, mkdir, writeFile } from 'node:fs/promises';
import { join, extname, dirname } from 'node:path';
import { fileURLToPath } from 'node:url';
import { chromium } from 'playwright';

const here = dirname(fileURLToPath(import.meta.url));
const [glb, outdir, shotsFile] = process.argv.slice(2);
const shots = JSON.parse(await readFile(shotsFile, 'utf8'));
const types = { '.html': 'text/html', '.js': 'text/javascript', '.glb': 'model/gltf-binary' };

const server = http.createServer(async (req, res) => {
  const p = req.url.startsWith('/model.') ? glb : join(here, decodeURIComponent(req.url.split('?')[0]));
  try {
    const data = await readFile(p);
    res.writeHead(200, { 'Content-Type': types[extname(p)] || 'application/octet-stream' });
    res.end(data);
  } catch {
    res.writeHead(404); res.end();
  }
}).listen(0);
const port = server.address().port;

const browser = await chromium.launch({
  args: ['--use-gl=angle', '--use-angle=swiftshader', '--enable-unsafe-swiftshader'],
});
const page = await browser.newPage({ viewport: { width: 768, height: 768 } });
page.on('console', (m) => m.type() === 'error' && console.error('page:', m.text()));
page.on('pageerror', (e) => console.error('pageerror:', e.message));
await page.goto(`http://localhost:${port}/index.html`);
await page.waitForFunction(() => window.ready);
const info = await page.evaluate((u) => window.load(u), '/model' + extname(glb));
await mkdir(outdir, { recursive: true });
await writeFile(join(outdir, 'info.json'), JSON.stringify(info, null, 2));
console.log(JSON.stringify({ ...info, bones: info.bones.length }, null, 1));
for (const s of shots) {
  if (s.track) {
    const pts = await page.evaluate(([c, b]) => window.track(c, b), [s.clip, s.track]);
    console.log(`track clip ${s.clip} ${s.track}:`, JSON.stringify(pts));
    continue;
  }
  if (s.sheet) {
    const url = await page.evaluate((v) => window.sheet(v), s);
    await writeFile(join(outdir, `${s.name}.png`), Buffer.from(url.split(',')[1], 'base64'));
    continue;
  }
  await page.evaluate((v) => window.view(v), s);
  await page.locator('canvas').screenshot({ path: join(outdir, `${s.name}.png`) });
}
await browser.close();
server.close();
