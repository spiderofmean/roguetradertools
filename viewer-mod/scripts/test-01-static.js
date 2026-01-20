/**
 * Test 01: Static sanity checks
 * Runs without the game - validates repo structure and configuration.
 */

const fs = require('fs');
const path = require('path');
const { execSync } = require('child_process');

const ROOT = path.join(__dirname, '..');

function fail(message) {
  console.error(`FAIL: ${message}`);
  process.exit(1);
}

function pass(message) {
  console.log(`PASS: ${message}`);
}

function checkFileExists(relativePath, description) {
  const fullPath = path.join(ROOT, relativePath);
  if (!fs.existsSync(fullPath)) {
    fail(`${description} not found at ${relativePath}`);
  }
  return fullPath;
}

function main() {
  console.log('Test 01: Static Sanity Checks\n');

  // 1. Check mod/Info.json exists and parses correctly
  const infoJsonPath = checkFileExists('mod/Info.json', 'Mod Info.json');
  try {
    const content = fs.readFileSync(infoJsonPath, 'utf8');
    const info = JSON.parse(content);
    
    if (!info.EntryMethod) {
      fail('Info.json is missing EntryMethod');
    }
    if (!info.Id) {
      fail('Info.json is missing Id');
    }
    pass(`Info.json parses correctly (EntryMethod: ${info.EntryMethod})`);
  } catch (err) {
    fail(`Failed to parse Info.json: ${err.message}`);
  }

  // 2. Check mod csproj exists
  checkFileExists('mod/ViewerMod.csproj', 'Mod project file');
  pass('ViewerMod.csproj exists');

  // 3. Check required source files exist
  const requiredFiles = [
    'mod/src/Entry.cs',
    'mod/src/ViewerBehaviour.cs',
    'mod/src/Server/HttpServer.cs',
    'mod/src/Server/Router.cs',
    'mod/src/State/HandleRegistry.cs',
    'mod/src/State/ObjectInspector.cs',
    'mod/src/State/RootProvider.cs',
    'mod/src/State/ImageExtractor.cs',
    'mod/src/Models/WireFormat.cs',
  ];

  for (const file of requiredFiles) {
    checkFileExists(file, file);
  }
  pass('All mod source files exist');

  // 4. Check client projects exist
  checkFileExists('client-web/package.json', 'Web client package.json');
  checkFileExists('mcp-server/package.json', 'MCP server package.json');
  checkFileExists('sdk/package.json', 'SDK package.json');
  pass('All client projects exist');

  // 5. Check TypeScript compiles (if node_modules exist)
  const sdkNodeModules = path.join(ROOT, 'sdk', 'node_modules');
  if (fs.existsSync(sdkNodeModules)) {
    try {
      execSync('npx tsc --noEmit', { cwd: path.join(ROOT, 'sdk'), stdio: 'pipe' });
      pass('SDK TypeScript compiles');
    } catch (err) {
      fail(`SDK TypeScript compilation failed: ${err.message}`);
    }
  } else {
    console.log('SKIP: SDK node_modules not installed, skipping TypeScript check');
  }

  console.log('\n✅ All static checks passed');
}

main();
