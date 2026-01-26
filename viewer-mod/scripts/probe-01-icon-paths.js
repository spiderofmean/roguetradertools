/**
 * Probe 01: Derive equipment icon member paths
 * Requires the game running with the mod loaded and a save open.
 *
 * Strategy:
 * - Use existing /api/blueprints/equipment/stream to sample equipment GUIDs.
 * - For each GUID, call POST /api/blueprints/handle { guid } to get a handleId.
 * - Use /api/inspect to traverse the live object graph and find the first Sprite/Texture.
 * - Record the member-path that led to it and output a histogram.
 */

const fs = require('fs');
const path = require('path');

const BASE_URL = 'http://localhost:5000';
const OUTPUT_DIR = path.join(__dirname, '..', 'test-output');

const DEFAULTS = {
  sampleCount: 40,
  scanStart: 0,
  scanCount: 600,
  maxNodes: 2500,
};

const ICON_HINTS = ['icon', 'iconsprite', 'sprite', 'texture', 'inventory', 'portrait'];

function fail(message) {
  console.error(`FAIL: ${message}`);
  process.exit(1);
}

function looksLikeIconName(name) {
  const lower = (name || '').toLowerCase();
  return ICON_HINTS.some((h) => lower.includes(h));
}

function isImageType(typeName) {
  const t = typeName || '';
  return t.includes('Sprite') || t.includes('Texture2D') || t.includes('Texture');
}

async function postJson(endpoint, body = {}) {
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

async function postText(endpoint, body = {}) {
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

async function getInspection(handleId) {
  return postJson('/api/inspect', { handleId });
}

async function getBlueprintHandle(guid) {
  return postJson('/api/blueprints/handle', { guid });
}

async function getIconPath(guid) {
  const response = await fetch(`${BASE_URL}/api/blueprints/equipment/icon-path/${guid}`, { method: 'GET' });
  if (response.status === 404) {
    // Normal case during probing: some blueprint types may not expose an icon.
    return { path: null, iconType: null, notFound: true };
  }
  if (!response.ok) {
    const text = await response.text();
    throw new Error(`HTTP ${response.status}: ${text}`);
  }
  return response.json();
}

async function clearHandles() {
  try {
    await postJson('/api/handles/clear', {});
  } catch {
    // ignore
  }
}

async function sampleEquipmentGuids({ scanStart, scanCount, sampleCount }) {
  const ndjson = await postText('/api/blueprints/equipment/stream', { start: scanStart, count: scanCount });
  const lines = ndjson.split('\n').filter(Boolean);

  const items = [];
  for (const line of lines) {
    try {
      const obj = JSON.parse(line);
      if (!obj?.meta?.Guid) continue;

      // Focus on actual item blueprints when deriving the shared icon path.
      const type = obj.meta.Type || '';
      if (!type.startsWith('BlueprintItem')) continue;

      const name = (obj.meta.Name || '').trim();
      if (!name) continue;

      items.push({ guid: obj.meta.Guid, type, name });
      if (items.length >= sampleCount) break;
    } catch {
      // ignore
    }
  }

  return items;
}

async function findFirstImagePath(startHandleId, { maxNodes }) {
  const visited = new Set();

  // We keep explicit paths (member-name chain). Example: root.m_Icon.m_Sprite
  const high = [];
  const low = [{ handleId: startHandleId, path: 'root' }];

  let processed = 0;

  while ((high.length > 0 || low.length > 0) && processed < maxNodes) {
    const node = high.length > 0 ? high.shift() : low.shift();
    processed++;

    if (!node?.handleId) continue;
    if (visited.has(node.handleId)) continue;
    visited.add(node.handleId);

    let insp;
    try {
      insp = await getInspection(node.handleId);
    } catch {
      continue;
    }

    if (isImageType(insp.type)) {
      return { found: true, path: node.path, type: insp.type, nodes: processed };
    }

    // Expand members
    if (Array.isArray(insp.members)) {
      for (const m of insp.members) {
        if (!m?.handleId) continue;

        const next = { handleId: m.handleId, path: `${node.path}.${m.name}` };
        if (looksLikeIconName(m.name)) high.unshift(next);
        else low.push(next);
      }
    }

    // Expand collections
    if (insp.collectionInfo?.elements && Array.isArray(insp.collectionInfo.elements)) {
      for (const e of insp.collectionInfo.elements) {
        if (!e?.handleId) continue;
        low.push({ handleId: e.handleId, path: `${node.path}[]` });
      }
    }
  }

  return { found: false, path: null, type: null, nodes: processed };
}

async function main() {
  const args = process.argv.slice(2);
  const opts = { ...DEFAULTS };

  for (let i = 0; i < args.length; i++) {
    const a = args[i];
    const v = args[i + 1];

    if (a === '--sample' && v) opts.sampleCount = parseInt(v, 10);
    if (a === '--start' && v) opts.scanStart = parseInt(v, 10);
    if (a === '--count' && v) opts.scanCount = parseInt(v, 10);
    if (a === '--maxNodes' && v) opts.maxNodes = parseInt(v, 10);
  }

  console.log('Probe 01: Derive equipment icon member paths');
  console.log(`Base URL: ${BASE_URL}`);
  console.log(`Scan: start=${opts.scanStart} count=${opts.scanCount} | sample=${opts.sampleCount} | maxNodes=${opts.maxNodes}\n`);

  if (!fs.existsSync(OUTPUT_DIR)) fs.mkdirSync(OUTPUT_DIR, { recursive: true });

  let samples;
  try {
    samples = await sampleEquipmentGuids(opts);
  } catch (err) {
    fail(`Failed to sample equipment GUIDs: ${err.message}`);
  }

  if (!samples.length) fail('No equipment GUIDs sampled (is a save loaded?)');

  const histogram = new Map();
  const byType = new Map();
  const results = [];

  for (let i = 0; i < samples.length; i++) {
    const s = samples[i];
    console.log(`[${i + 1}/${samples.length}] ${s.guid} ${s.type} ${s.name}`);

    try {
      const icon = await getIconPath(s.guid);
      results.push({ ...s, iconPath: icon.path, iconType: icon.iconType, notFound: !!icon.notFound });

      const key = icon.path || '<NOT_FOUND>';
      histogram.set(key, (histogram.get(key) || 0) + 1);

      const typeKey = s.type || '<UNKNOWN_TYPE>';
      if (!byType.has(typeKey)) byType.set(typeKey, { total: 0, paths: new Map() });
      const entry = byType.get(typeKey);
      entry.total++;
      entry.paths.set(key, (entry.paths.get(key) || 0) + 1);
    } catch (err) {
      results.push({ ...s, error: err.message });
      histogram.set('<ERROR>', (histogram.get('<ERROR>') || 0) + 1);
    }
  }

  const sorted = [...histogram.entries()].sort((a, b) => b[1] - a[1]);

  console.log('\n=== Path Histogram (top 15) ===');
  for (const [p, c] of sorted.slice(0, 15)) {
    console.log(`${c.toString().padStart(4)}  ${p}`);
  }

  const report = {
    generatedAt: new Date().toISOString(),
    options: opts,
    summary: {
      total: results.length,
      found: results.filter((r) => r.found).length,
      notFound: results.filter((r) => r.found === false).length,
      errors: results.filter((r) => r.error).length,
    },
    histogram: sorted.map(([p, c]) => ({ path: p, count: c })),
    results,
    byType: [...byType.entries()].map(([type, info]) => ({
      type,
      total: info.total,
      histogram: [...info.paths.entries()].sort((a, b) => b[1] - a[1]).map(([p, c]) => ({ path: p, count: c })),
    })),
  };

  const outPath = path.join(OUTPUT_DIR, 'probe-01-icon-paths-report.json');
  fs.writeFileSync(outPath, JSON.stringify(report, null, 2));
  console.log(`\nWrote report: ${outPath}`);
}

main().catch((err) => fail(`Unexpected error: ${err.message}`));
