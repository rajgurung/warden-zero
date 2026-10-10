// usage: node compare.mjs out.png bg img1.png img2.png ...  (side by side, scaled to equal height)
import { readFile, writeFile } from 'node:fs/promises';
import { chromium } from 'playwright';
const [out, bg, ...imgs] = process.argv.slice(2);
const urls = await Promise.all(imgs.map(async (p) => (p.endsWith('.png') ? 'data:image/png;base64,' : 'data:image/jpeg;base64,') + (await readFile(p)).toString('base64')));
const browser = await chromium.launch();
const page = await browser.newPage();
const data = await page.evaluate(async ([urls, bg]) => {
  const ims = await Promise.all(urls.map((u) => new Promise((r) => { const i = new Image(); i.onload = () => r(i); i.src = u; })));
  const H = 900, ws = ims.map((i) => Math.round((i.width * H) / i.height));
  const c = document.createElement('canvas');
  c.width = ws.reduce((a, b) => a + b, 0); c.height = H;
  const x = c.getContext('2d');
  x.fillStyle = bg; x.fillRect(0, 0, c.width, H);
  let off = 0;
  ims.forEach((im, k) => { x.drawImage(im, off, 0, ws[k], H); off += ws[k]; });
  return c.toDataURL('image/png');
}, [urls, bg]);
await writeFile(out, Buffer.from(data.split(',')[1], 'base64'));
await browser.close();
