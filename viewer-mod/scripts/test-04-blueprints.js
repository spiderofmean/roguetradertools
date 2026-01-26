/**
 * Test 04: Blueprints API
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

async function get(endpoint) {
  const response = await fetch(`${BASE_URL}${endpoint}`, { method: 'GET' });
  if (!response.ok) {
    const text = await response.text();
    throw new Error(`HTTP ${response.status}: ${text}`);
  }
  return response.json();
}

async function main() {
  console.log('Test 04: Blueprints API');
  console.log(`Base URL: ${BASE_URL}\n`);

  // 1) List
  let list;
  try {
    list = await get('/api/blueprints');
  } catch (err) {
    fail(`/api/blueprints failed: ${err.message}`);
  }

  if (!list || !Array.isArray(list.blueprints)) {
    fail('/api/blueprints did not return { blueprints: [...] }');
  }

  if (list.blueprints.length < 1000) {
    fail(`/api/blueprints returned too few entries: ${list.blueprints.length}`);
  }

  pass(`/api/blueprints returned ${list.blueprints.length} entries`);

  const first = list.blueprints[0];
  if (!first || !first.Guid) {
    fail('First blueprint entry missing Guid');
  }

  // 2) Detail
  let detail;
  try {
    detail = await get(`/api/blueprints/${first.Guid}`);
  } catch (err) {
    fail(`/api/blueprints/{guid} failed: ${err.message}`);
  }

  if (!detail || !detail.meta || !detail.meta.Guid) {
    fail('Detail response missing meta.Guid');
  }

  if (detail.meta.Guid.replace(/-/g, '').toLowerCase() !== first.Guid.replace(/-/g, '').toLowerCase()) {
    fail(`Detail meta.Guid mismatch: list=${first.Guid} detail=${detail.meta.Guid}`);
  }

  if (detail.data == null) {
    fail('Detail response missing data');
  }

  pass('Blueprint detail returned meta + data');

  console.log('\n✅ Blueprints API test passed');
}

main().catch((err) => {
  fail(`Unexpected error: ${err.message}`);
});
