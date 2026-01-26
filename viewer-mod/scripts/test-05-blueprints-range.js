/**
 * Test 05: Blueprints Range API
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

  return response.json();
}

async function main() {
  console.log('Test 05: Blueprints Range API');
  console.log(`Base URL: ${BASE_URL}\n`);

  let result;
  try {
    result = await post('/api/blueprints/range', { start: 0, count: 3 });
  } catch (err) {
    fail(`/api/blueprints/range failed: ${err.message}`);
  }

  if (!result || typeof result.total !== 'number') fail('Response missing total');
  if (result.total < 1000) fail(`Suspicious total: ${result.total}`);

  if (!Array.isArray(result.blueprints)) fail('Response missing blueprints array');
  if (result.blueprints.length !== 3) fail(`Expected 3 blueprints, got ${result.blueprints.length}`);

  const first = result.blueprints[0];
  if (!first || !first.meta || !first.meta.Guid) fail('First blueprint missing meta.Guid');
  if (first.data == null) fail('First blueprint missing data');

  pass('Range API returned meta + data for 3 blueprints');
  console.log('\n✅ Blueprints range API test passed');
}

main().catch((err) => fail(`Unexpected error: ${err.message}`));
