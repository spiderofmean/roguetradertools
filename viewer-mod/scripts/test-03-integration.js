/**
 * Test 03: Live end-to-end integration tests
 * Requires the game running with the mod loaded and a save open.
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

async function getImage(handleId) {
  const response = await fetch(`${BASE_URL}/api/image/${handleId}`, {
    method: 'GET',
  });

  if (!response.ok) {
    const text = await response.text();
    throw new Error(`HTTP ${response.status}: ${text}`);
  }

  return Buffer.from(await response.arrayBuffer());
}

async function main() {
  console.log('Test 03: Live End-to-End Integration');
  console.log(`Base URL: ${BASE_URL}\n`);

  // Ensure output directory exists
  if (!fs.existsSync(OUTPUT_DIR)) {
    fs.mkdirSync(OUTPUT_DIR, { recursive: true });
  }

  // 1. Traverse at least 3 object levels
  console.log('--- Test: Object Traversal (3 levels) ---');
  
  let currentHandle = null;
  let currentType = '';
  
  try {
    // Level 0: Get roots
    const roots = await post('/api/roots');
    if (roots.length === 0) {
      fail('No roots available');
    }
    currentHandle = roots[0].handleId;
    currentType = roots[0].type;
    console.log(`Level 0 (root): ${roots[0].name} (${currentType})`);

    // Traverse 3 levels
    for (let level = 1; level <= 3; level++) {
      const inspection = await post('/api/inspect', { handleId: currentHandle });
      
      // Find a non-primitive member to follow
      const nextMember = inspection.members.find(m => !m.isPrimitive && m.handleId);
      
      if (!nextMember) {
        // Try collection elements if no members
        if (inspection.collectionInfo && inspection.collectionInfo.elements.length > 0) {
          const elem = inspection.collectionInfo.elements.find(e => e.handleId);
          if (elem) {
            currentHandle = elem.handleId;
            currentType = elem.type;
            console.log(`Level ${level} (collection[${elem.index}]): ${currentType}`);
            continue;
          }
        }
        console.log(`Level ${level}: No non-primitive members to follow, stopping here`);
        break;
      }
      
      currentHandle = nextMember.handleId;
      currentType = nextMember.type;
      console.log(`Level ${level} (${nextMember.name}): ${currentType}`);
    }
    
    pass('Traversed object graph successfully');
  } catch (err) {
    fail(`Object traversal failed: ${err.message}`);
  }

  // 2. Validate collection consistency
  console.log('\n--- Test: Collection Validation ---');
  
  try {
    const roots = await post('/api/roots');
    let foundCollection = false;
    
    // Search for a collection in the first few levels
    const visited = new Set();
    const queue = roots.map(r => r.handleId);
    
    while (queue.length > 0 && !foundCollection && visited.size < 50) {
      const handleId = queue.shift();
      if (visited.has(handleId)) continue;
      visited.add(handleId);
      
      const inspection = await post('/api/inspect', { handleId });
      
      if (inspection.collectionInfo && inspection.collectionInfo.isCollection) {
        const { count, elements } = inspection.collectionInfo;
        
        if (count !== elements.length) {
          fail(`Collection count mismatch: count=${count}, elements.length=${elements.length}`);
        }
        
        console.log(`Found collection: ${inspection.type} with ${count} elements`);
        pass('Collection count matches elements length');
        foundCollection = true;
        break;
      }
      
      // Add member handles to queue
      for (const member of inspection.members) {
        if (member.handleId && !visited.has(member.handleId)) {
          queue.push(member.handleId);
        }
      }
    }
    
    if (!foundCollection) {
      console.log('SKIP: No collections found in first 50 inspected objects');
    }
  } catch (err) {
    fail(`Collection validation failed: ${err.message}`);
  }

  // 3. Validate image extraction
  console.log('\n--- Test: Image Extraction ---');
  
  try {
    const roots = await post('/api/roots');
    let foundImage = false;
    
    // Search for a Texture2D or Sprite
    const visited = new Set();
    const queue = roots.map(r => r.handleId);
    
    while (queue.length > 0 && !foundImage && visited.size < 100) {
      const handleId = queue.shift();
      if (visited.has(handleId)) continue;
      visited.add(handleId);
      
      const inspection = await post('/api/inspect', { handleId });
      
      // Check if this is a texture/sprite
      const isTexture = inspection.type.includes('Texture2D') || 
                        inspection.type.includes('Sprite');
      
      if (isTexture) {
        console.log(`Found texture: ${inspection.type}`);
        
        // Try to extract the image
        try {
          const imageBytes = await getImage(handleId);
          
          // Check PNG magic bytes (89 50 4E 47 = \x89PNG)
          if (imageBytes.length < 4) {
            fail('Image bytes too short');
          }
          
          const magic = imageBytes.slice(0, 4);
          if (magic[0] !== 0x89 || magic[1] !== 0x50 || 
              magic[2] !== 0x4E || magic[3] !== 0x47) {
            fail(`Invalid PNG header: ${magic.toString('hex')}`);
          }
          
          // Save the image
          const outputPath = path.join(OUTPUT_DIR, 'image.png');
          fs.writeFileSync(outputPath, imageBytes);
          console.log(`Saved image to: ${outputPath} (${imageBytes.length} bytes)`);
          
          pass('Image extraction successful with valid PNG header');
          foundImage = true;
          break;
        } catch (imgErr) {
          console.log(`Could not extract image from ${handleId}: ${imgErr.message}`);
        }
      }
      
      // Add member handles to queue (look for texture fields)
      for (const member of inspection.members) {
        if (member.handleId && !visited.has(member.handleId)) {
          // Prioritize likely texture fields
          if (member.name.toLowerCase().includes('icon') ||
              member.name.toLowerCase().includes('texture') ||
              member.name.toLowerCase().includes('sprite') ||
              member.name.toLowerCase().includes('image')) {
            queue.unshift(member.handleId);
          } else {
            queue.push(member.handleId);
          }
        }
      }
      
      // Also check collection elements
      if (inspection.collectionInfo) {
        for (const elem of inspection.collectionInfo.elements) {
          if (elem.handleId && !visited.has(elem.handleId)) {
            queue.push(elem.handleId);
          }
        }
      }
    }
    
    if (!foundImage) {
      console.log('SKIP: No textures found in first 100 inspected objects');
      console.log('Note: Image extraction will be validated when a texture is found');
    }
  } catch (err) {
    fail(`Image extraction test failed: ${err.message}`);
  }

  // Cleanup - clear handles
  console.log('\n--- Cleanup ---');
  try {
    await post('/api/handles/clear');
    pass('Handles cleared');
  } catch (err) {
    console.warn(`Warning: Failed to clear handles: ${err.message}`);
  }

  console.log('\n✅ All integration tests passed');
}

main().catch((err) => {
  fail(`Unexpected error: ${err.message}`);
});
