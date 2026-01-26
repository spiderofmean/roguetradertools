#!/usr/bin/env node
// Dumps equipment-like blueprints (extracter-mod style) to a single NDJSON file.

const http = require('http');
const fs = require('fs');
const path = require('path');

const BASE_URL = 'http://localhost:5000';
const OUTPUT_DIR = path.resolve(__dirname, '..', 'blueprint-dump');
const SCAN_BATCH_SIZE = 20000;

const agent = new http.Agent({ keepAlive: true, maxSockets: 1, maxFreeSockets: 1 });

function httpPostJson(url, body) {
    return new Promise((resolve, reject) => {
        const json = JSON.stringify(body);
        const req = http.request(url, {
            method: 'POST',
            agent,
            headers: {
                'Content-Type': 'application/json',
                'Content-Length': Buffer.byteLength(json),
            },
        }, res => {
            let data = '';
            res.on('data', chunk => data += chunk);
            res.on('end', () => {
                if (res.statusCode !== 200) reject(new Error(`HTTP ${res.statusCode}: ${data}`));
                else resolve(JSON.parse(data));
            });
        });

        req.on('error', reject);
        req.write(json);
        req.end();
    });
}

function httpPostStream(url, body, writable) {
    return new Promise((resolve, reject) => {
        const json = JSON.stringify(body);
        const req = http.request(url, {
            method: 'POST',
            agent,
            headers: {
                'Content-Type': 'application/json',
                'Content-Length': Buffer.byteLength(json),
            },
        }, res => {
            if (res.statusCode !== 200) {
                let data = '';
                res.on('data', chunk => data += chunk);
                res.on('end', () => reject(new Error(`HTTP ${res.statusCode}: ${data}`)));
                return;
            }

            res.pipe(writable, { end: false });
            res.on('end', resolve);
            res.on('error', reject);
        });

        req.on('error', reject);
        req.write(json);
        req.end();
    });
}

async function main() {
    fs.mkdirSync(OUTPUT_DIR, { recursive: true });

    console.log('Fetching blueprint GUID total...');
    const total = (await httpPostJson(`${BASE_URL}/api/blueprints/range`, { start: 0, count: 0 })).total;
    console.log(`Total GUIDs: ${total}`);

    const outPath = path.join(OUTPUT_DIR, 'equipment.jsonl');
    fs.writeFileSync(outPath, '');
    const out = fs.createWriteStream(outPath, { flags: 'a' });

    console.log('Streaming equipment blueprints...');
    let scanned = 0;
    for (let start = 0; start < total; start += SCAN_BATCH_SIZE) {
        const count = Math.min(SCAN_BATCH_SIZE, total - start);
        await httpPostStream(`${BASE_URL}/api/blueprints/equipment/stream`, { start, count }, out);
        scanned += count;
        console.log(`${scanned}/${total}`);
    }

    await new Promise((resolve) => out.end(resolve));
    console.log(`Done. Output: ${outPath}`);
}

main().catch(e => { console.error(e.message); process.exit(1); });
