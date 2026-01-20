/**
 * Test 02: Live connectivity checks
 * Requires the game running with the mod loaded and a save open.
 */

const fs = require('fs');
const path = require('path');

// Load config if it exists
const CONFIG_PATH = path.join(__dirname, 'test-config.json');
let config = { baseUrl: 'http://localhost:5000' };

if (fs.existsSync(CONFIG_PATH)) {
  try {
    config = JSON.parse(fs.readFileSync(CONFIG_PATH, 'utf8'));
    console.log(`Using config from test-config.json: ${config.baseUrl}`);
  } catch (err) {
    console.warn(`Warning: Failed to parse test-config.json: ${err.message}`);
  }
}

const BASE_URL = config.baseUrl;

function fail(message) {
  console.error(`FAIL: ${message}`);
  process.exit(1);
}

function pass(message) {
  console.log(`PASS: ${message}`);
}

async function post(endpoint, body = {}) {
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
  console.log('Test 02: Live Connectivity Checks');
  console.log(`Base URL: ${BASE_URL}\n`);

  // 1. POST /api/roots should return a non-empty array
  let roots;
  try {
    roots = await post('/api/roots');
    
    if (!Array.isArray(roots)) {
      fail('/api/roots did not return an array');
    }
    if (roots.length === 0) {
      fail('/api/roots returned empty array - is a save loaded?');
    }
    
    pass(`/api/roots returned ${roots.length} root(s)`);
    console.log(`  First root: ${roots[0].name} (${roots[0].type})`);
  } catch (err) {
    fail(`/api/roots failed: ${err.message}`);
  }

  // 2. POST /api/inspect on the first root should return an object with members
  let inspectResult;
  try {
    const firstRoot = roots[0];
    inspectResult = await post('/api/inspect', { handleId: firstRoot.handleId });
    
    if (!inspectResult.handleId) {
      fail('/api/inspect response missing handleId');
    }
    if (!inspectResult.type) {
      fail('/api/inspect response missing type');
    }
    if (!Array.isArray(inspectResult.members)) {
      fail('/api/inspect response missing members array');
    }
    
    pass(`/api/inspect returned object with ${inspectResult.members.length} members`);
    console.log(`  Type: ${inspectResult.type}`);
  } catch (err) {
    fail(`/api/inspect failed: ${err.message}`);
  }

  // 3. POST /api/handles/clear should return { cleared: true }
  try {
    const clearResult = await post('/api/handles/clear');
    
    if (clearResult.cleared !== true) {
      fail('/api/handles/clear did not return { cleared: true }');
    }
    
    pass('/api/handles/clear succeeded');
  } catch (err) {
    fail(`/api/handles/clear failed: ${err.message}`);
  }

  console.log('\n✅ All connectivity checks passed');
}

main().catch((err) => {
  fail(`Unexpected error: ${err.message}`);
});
