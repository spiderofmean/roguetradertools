/**
 * Test 07: Equipment icon extraction
 * Requires the game running with the mod loaded and a save open.
 *
 * Calls POST /api/blueprints/equipment/stream to find a blueprint GUID,
 * then calls GET /api/blueprints/equipment/icon/{guid} and writes a PNG.
 */

const fs = require('fs');
const path = require('path');

const BASE_URL = 'http://localhost:5000';
const OUTPUT_DIR = path.join(__dirname, '..', 'test-output');

function fail(message) {
  console.error(`FAIL: ${message}`);
  process.exit(1);
}

function pass(message) {
  console.log(`PASS: ${message}`);
}

async function postText(endpoint, body) {
  const response = await fetch(`${BASE_URL}${endpoint}`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(body),
  });

  if (!response.ok) {
    const text = await response.text();
    throw new Error(`HTTP ${response.status}: ${text}`);
  }

  return response.text();
}

async function getBytes(endpoint) {
  const response = await fetch(`${BASE_URL}${endpoint}`, { method: 'GET' });
  if (!response.ok) {
    const text = await response.text();
    throw new Error(`HTTP ${response.status}: ${text}`);
  }
  return Buffer.from(await response.arrayBuffer());
}

function isPng(buf) {
  return buf && buf.length >= 4 && buf[0] === 0x89 && buf[1] === 0x50 && buf[2] === 0x4e && buf[3] === 0x47;
}

async function main() {
  console.log('Test 07: Equipment icon extraction');
  console.log(`Base URL: ${BASE_URL}\n`);

  if (!fs.existsSync(OUTPUT_DIR)) {
    fs.mkdirSync(OUTPUT_DIR, { recursive: true });
  }

  let ndjson;
  try {
    ndjson = await postText('/api/blueprints/equipment/stream', { start: 0, count: 500 });
  } catch (err) {
    fail(`/api/blueprints/equipment/stream failed: ${err.message}`);
  }

  const lines = ndjson.split('\n').filter(Boolean);
  if (lines.length === 0) fail('Stream returned no NDJSON lines');

  // Find the first entry that has a meta.Guid
  let guid = null;
  for (const line of lines) {
    try {
      const obj = JSON.parse(line);
      if (obj && obj.meta && obj.meta.Guid) {
        guid = obj.meta.Guid;
        break;
      }
    } catch {
      // ignore
    }
  }

  if (!guid) fail('Could not find any equipment blueprint meta.Guid in stream');
  pass(`Picked equipment blueprint GUID: ${guid}`);

  let png;
  try {
    png = await getBytes(`/api/blueprints/equipment/icon/${guid}`);
  } catch (err) {
    fail(`/api/blueprints/equipment/icon/{guid} failed: ${err.message}`);
  }

  if (!isPng(png)) {
    fail(`Icon endpoint did not return PNG bytes (first 8 = ${png.slice(0, 8).toString('hex')})`);
  }

  const outPath = path.join(OUTPUT_DIR, `equipment-icon-${guid}.png`);
  fs.writeFileSync(outPath, png);
  pass(`Wrote icon PNG: ${outPath} (${png.length} bytes)`);

  console.log('\n✅ Equipment icon extraction test passed');
}

main().catch((err) => fail(`Unexpected error: ${err.message}`));
