#!/usr/bin/env node
// Minimal Tripo 3D API (v3) client. No dependencies; needs Node 20+.
// Reads the API key from TRIPO_API_KEY. Never prints the key.
// See tools/tripo/README.md for usage.
import { readFile, writeFile, mkdir } from 'node:fs/promises';
import { basename, extname, join } from 'node:path';

const BASE = process.env.TRIPO_API_BASE || 'https://openapi.tripo3d.ai/v3';
const KEY = process.env.TRIPO_API_KEY;

function usage() {
  console.log(`usage: tripo.mjs <command> [args]
  balance                         show available and frozen credits
  upload <file>                   upload an image/model, print file_token
  create <endpoint> <json|@file>  create a task, e.g. create generation/image-to-model @body.json
  poll <task_id> [timeout_s]      wait for a task to finish, print status and credits
  download <task_id> <dir>        download every *_url in the task output into <dir>`);
  process.exit(1);
}

async function api(path, init = {}) {
  if (!KEY) throw new Error('TRIPO_API_KEY is not set');
  const res = await fetch(`${BASE}/${path}`, {
    ...init,
    headers: { Authorization: `Bearer ${KEY}`, ...init.headers },
  });
  const body = await res.json().catch(() => ({}));
  if (!res.ok || body.code !== 0) {
    throw new Error(`${path}: HTTP ${res.status} ${JSON.stringify(body)}`);
  }
  return body.data;
}

async function balance() {
  const d = await api('account/balance');
  console.log(`balance=${d.balance} frozen=${d.frozen}`);
  return d;
}

async function upload(file) {
  const form = new FormData();
  const type = extname(file).toLowerCase() === '.png' ? 'image/png' : 'application/octet-stream';
  form.append('file', new Blob([await readFile(file)], { type }), basename(file));
  const d = await api('files', { method: 'POST', body: form });
  console.log(`file_token=${d.file_token}`);
}

async function create(endpoint, json) {
  const body = json.startsWith('@') ? await readFile(json.slice(1), 'utf8') : json;
  JSON.parse(body); // fail early on bad JSON, before spending anything
  await balance();
  const d = await api(endpoint, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body,
  });
  console.log(`task_id=${d.task_id}`);
}

async function poll(taskId, timeoutS = 600) {
  const start = Date.now();
  for (;;) {
    const t = await api(`tasks/${taskId}`);
    if (!['queued', 'running'].includes(t.status)) {
      console.log(`status=${t.status} credits_consumed=${t.credits_consumed ?? '?'}`);
      if (t.error_code) console.log(`error=${t.error_code} ${t.error_message ?? ''}`);
      if (t.output) console.log(`output keys: ${Object.keys(t.output).join(', ')}`);
      return t;
    }
    if (Date.now() - start > timeoutS * 1000) throw new Error(`timed out (status=${t.status})`);
    process.stdout.write(`${t.status} ${t.progress ?? 0}%\r`);
    await new Promise((r) => setTimeout(r, 3000));
  }
}

// Output URLs are short-lived signed links, so download right after the task succeeds.
async function download(taskId, dir) {
  const t = await api(`tasks/${taskId}`);
  if (t.status !== 'success') throw new Error(`task is ${t.status}`);
  await mkdir(dir, { recursive: true });
  const meta = { ...t, output: {} };
  for (const [key, value] of Object.entries(t.output)) {
    if (typeof value !== 'string' || !value.startsWith('http')) {
      meta.output[key] = value;
      continue;
    }
    const ext = extname(new URL(value).pathname) || '.bin';
    const file = join(dir, `${key.replace(/_url$/, '')}${ext}`);
    const res = await fetch(value);
    if (!res.ok) throw new Error(`download ${key}: HTTP ${res.status}`);
    await writeFile(file, Buffer.from(await res.arrayBuffer()));
    meta.output[key] = basename(file);
    console.log(`${key} -> ${file}`);
  }
  // Task record without the expiring URLs, for provenance.
  await writeFile(join(dir, `${taskId}.json`), JSON.stringify(meta, null, 2) + '\n');
}

const [cmd, ...args] = process.argv.slice(2);
const commands = {
  balance: () => balance(),
  upload: () => args[0] ? upload(args[0]) : usage(),
  create: () => args.length === 2 ? create(args[0], args[1]) : usage(),
  poll: () => args[0] ? poll(args[0], Number(args[1]) || undefined) : usage(),
  download: () => args.length === 2 ? download(args[0], args[1]) : usage(),
};
if (!commands[cmd]) usage();
commands[cmd]().catch((e) => {
  console.error(e.message);
  process.exit(1);
});
