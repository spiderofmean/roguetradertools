#!/usr/bin/env node
import { Server } from '@modelcontextprotocol/sdk/server/index.js';
import { StdioServerTransport } from '@modelcontextprotocol/sdk/server/stdio.js';
import {
  CallToolRequestSchema,
  ListToolsRequestSchema,
  Tool,
} from '@modelcontextprotocol/sdk/types.js';
import { GameClient, RootEntry, InspectResponse } from './sdk/index.js';

// Configuration
const DEFAULT_BASE_URL = 'http://localhost:5000';

// Create game client
const client = new GameClient({
  baseUrl: process.env.VIEWER_MOD_URL ?? DEFAULT_BASE_URL,
});

// Define available tools
const tools: Tool[] = [
  {
    name: 'game_list_roots',
    description: 'Get the list of root entry points into the game\'s object graph. Returns an array of root objects that can be inspected.',
    inputSchema: {
      type: 'object',
      properties: {},
      required: [],
    },
  },
  {
    name: 'game_inspect_object',
    description: 'Inspect a game object by its handle ID. Returns the object\'s type, value, members (fields/properties), and collection info if applicable. Use handleId values from roots or from other inspect calls to navigate the object graph.',
    inputSchema: {
      type: 'object',
      properties: {
        handleId: {
          type: 'string',
          description: 'The handle ID (GUID) of the object to inspect',
        },
      },
      required: ['handleId'],
    },
  },
  {
    name: 'game_clear_handles',
    description: 'Clear all handles from the server\'s registry. This releases references to all tracked objects. Use this to free memory or reset the inspection state.',
    inputSchema: {
      type: 'object',
      properties: {},
      required: [],
    },
  },
];

// Format root entry for display
function formatRoot(root: RootEntry): string {
  return `- ${root.name}\n  Handle: ${root.handleId}\n  Type: ${root.type}\n  Assembly: ${root.assemblyName}`;
}

// Format inspect response for display
function formatInspectResponse(response: InspectResponse): string {
  const lines: string[] = [];
  
  lines.push(`Object: ${response.type}`);
  lines.push(`Handle: ${response.handleId}`);
  lines.push(`Assembly: ${response.assemblyName}`);
  lines.push(`Value: ${response.value ?? 'null'}`);
  lines.push('');
  
  // Members
  if (response.members.length > 0) {
    lines.push('Members:');
    for (const member of response.members) {
      const valueStr = member.value === null ? 'null' : String(member.value);
      if (member.isPrimitive) {
        lines.push(`  ${member.name}: ${valueStr} (${member.type})`);
      } else if (member.handleId) {
        lines.push(`  ${member.name}: [${member.type}] -> ${member.handleId}`);
      } else {
        lines.push(`  ${member.name}: null (${member.type})`);
      }
    }
  }
  
  // Collection info
  if (response.collectionInfo) {
    const col = response.collectionInfo;
    lines.push('');
    lines.push(`Collection: ${col.count} elements of ${col.elementType}`);
    
    // Show first few elements
    const maxShow = Math.min(10, col.elements.length);
    for (let i = 0; i < maxShow; i++) {
      const elem = col.elements[i];
      const valueStr = elem.value === null ? 'null' : String(elem.value);
      if (elem.handleId) {
        lines.push(`  [${elem.index}]: ${valueStr} -> ${elem.handleId}`);
      } else {
        lines.push(`  [${elem.index}]: ${valueStr}`);
      }
    }
    
    if (col.elements.length > maxShow) {
      lines.push(`  ... and ${col.elements.length - maxShow} more elements`);
    }
  }
  
  return lines.join('\n');
}

// Create the MCP server
const server = new Server(
  {
    name: 'viewer-mod-mcp',
    version: '1.0.0',
  },
  {
    capabilities: {
      tools: {},
    },
  }
);

// Handle list tools request
server.setRequestHandler(ListToolsRequestSchema, async () => {
  return { tools };
});

// Handle tool calls
server.setRequestHandler(CallToolRequestSchema, async (request) => {
  const { name, arguments: args } = request.params;
  
  try {
    switch (name) {
      case 'game_list_roots': {
        const roots = await client.getRoots();
        if (roots.length === 0) {
          return {
            content: [
              {
                type: 'text',
                text: 'No roots found. Make sure the game is running with a save loaded.',
              },
            ],
          };
        }
        
        const formatted = roots.map(formatRoot).join('\n\n');
        return {
          content: [
            {
              type: 'text',
              text: `Found ${roots.length} root(s):\n\n${formatted}`,
            },
          ],
        };
      }
      
      case 'game_inspect_object': {
        const handleId = (args as { handleId: string }).handleId;
        if (!handleId) {
          return {
            content: [
              {
                type: 'text',
                text: 'Error: handleId is required',
              },
            ],
            isError: true,
          };
        }
        
        const response = await client.inspect(handleId);
        return {
          content: [
            {
              type: 'text',
              text: formatInspectResponse(response),
            },
          ],
        };
      }
      
      case 'game_clear_handles': {
        await client.clearHandles();
        return {
          content: [
            {
              type: 'text',
              text: 'All handles cleared successfully.',
            },
          ],
        };
      }
      
      default:
        return {
          content: [
            {
              type: 'text',
              text: `Unknown tool: ${name}`,
            },
          ],
          isError: true,
        };
    }
  } catch (error) {
    const errorMessage = error instanceof Error ? error.message : String(error);
    return {
      content: [
        {
          type: 'text',
          text: `Error: ${errorMessage}`,
        },
      ],
      isError: true,
    };
  }
});

// Start the server
async function main() {
  const transport = new StdioServerTransport();
  await server.connect(transport);
  console.error('Viewer Mod MCP Server started');
}

main().catch((error) => {
  console.error('Fatal error:', error);
  process.exit(1);
});
