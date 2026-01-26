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

    // --- New: Fetch icons for each item ---
    const readline = require('readline');
    const https = require('https');
    const http = require('http');

    console.log('Fetching icons for each equipment item...');
    const rl = readline.createInterface({
        input: fs.createReadStream(outPath),
        crlfDelay: Infinity
    });

    let iconCount = 0, iconFail = 0;
    function fetchIcon(url, destPath) {
        return new Promise((resolve, reject) => {
            const mod = url.startsWith('https:') ? https : http;
            mod.get(url, res => {
                if (res.statusCode !== 200) {
                    res.resume();
                    return reject(new Error(`HTTP ${res.statusCode}`));
                }
                const file = fs.createWriteStream(destPath);
                res.pipe(file);
                file.on('finish', () => file.close(resolve));
                file.on('error', err => {
                    fs.unlink(destPath, () => reject(err));
                });
            }).on('error', reject);
        });
    }

    for await (const line of rl) {
        if (!line.trim()) continue;
        let obj;
        try {
            obj = JSON.parse(line);
        } catch (e) {
            iconFail++;
            continue;
        }
        const guid = obj && obj.meta && obj.meta.Guid;
        if (!guid) {
            iconFail++;
            continue;
        }
        const iconPath = path.join(OUTPUT_DIR, `${guid}.png`);
        if (fs.existsSync(iconPath)) continue; // skip if already exists
        try {
            await fetchIcon(`${BASE_URL}/api/blueprints/equipment/icon/${guid}`, iconPath);
            iconCount++;
            if (iconCount % 50 === 0) console.log(`  Saved ${iconCount} icons...`);
        } catch (e) {
            iconFail++;
        }
    }
    console.log(`Icon fetch complete. Saved: ${iconCount}, failed: ${iconFail}`);
}

main().catch(e => { console.error(e.message); process.exit(1); });
