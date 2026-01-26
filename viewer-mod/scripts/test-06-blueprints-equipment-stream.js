/**
 * Test 06: Equipment Blueprint Stream API
 * Requires the game running with the mod loaded and a save open.
 */

const BASE_URL = 'http://localhost:5000';

function fail(message) {
  console.error(`FAIL: ${message}`);
  process.exit(1);
}

function pass(message) {
  console.log(`PASS: ${message}`);
}

async function post(endpoint, body) {
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

async function main() {
  console.log('Test 06: Equipment Blueprint Stream API');
  console.log(`Base URL: ${BASE_URL}\n`);

  let text;
  try {
    text = await post('/api/blueprints/equipment/stream', { start: 0, count: 200 });
  } catch (err) {
    fail(`/api/blueprints/equipment/stream failed: ${err.message}`);
  }

  const lines = text.split('\n').filter(Boolean);
  if (lines.length === 0) fail('Stream returned no NDJSON lines');

  let first;
  try {
    first = JSON.parse(lines[0]);
  } catch {
    fail('First NDJSON line is not valid JSON');
  }

  if (!first.meta || !first.meta.Guid) fail('First line missing meta.Guid');
  if (first.data == null) fail('First line missing data');

  pass(`Stream returned ${lines.length} line(s) for scan of 200 GUIDs`);
  console.log('\n✅ Equipment stream API test passed');
}

main().catch((err) => fail(`Unexpected error: ${err.message}`));
