# Viewer Mod

A live, read-only game state viewer for Warhammer 40,000: Rogue Trader. Exposes the running game's object graph via HTTP+JSON for exploration by both human (web UI) and agent (MCP) clients.

## Quick Start

### 1. Build the Mod

```powershell
cd viewer-mod
.\scripts\build.ps1
```

### 2. Deploy to Game

```powershell
.\scripts\deploy.ps1
```

Or manually copy:
- `mod/bin/Release/net472/ViewerMod.dll` → `<GameDir>/Mods/ViewerMod/`
- `mod/Info.json` → `<GameDir>/Mods/ViewerMod/`

### 3. Start the Game

Launch Rogue Trader with UnityModManager. The mod will start an HTTP server on `http://localhost:5000`.

### 4. Run Tests

With the game running and a save loaded:

```bash
# Static checks (no game needed)
node scripts/test-01-static.js

# Connectivity checks (game required)
node scripts/test-02-connectivity.js

# Full integration tests (game required)
node scripts/test-03-integration.js
```

## Components

### Mod Server (C#)
Location: `mod/`

The in-game HTTP server that:
- Maintains a handle registry (GUID → object references)
- Reflects on objects to return their members
- Provides entry points via root objects
- Extracts image data from textures/sprites

API Endpoints:
- `POST /api/roots` - Get entry point objects
- `POST /api/inspect` - Inspect object by handle ID
- `GET /api/image/{handleId}` - Get PNG image bytes
- `POST /api/handles/clear` - Clear all handles

### SDK (TypeScript)
Location: `sdk/`

Shared client library for communicating with the game server:

```typescript
import { GameClient } from '@viewer-mod/sdk';

const client = new GameClient({ baseUrl: 'http://localhost:5000' });
const roots = await client.getRoots();
const data = await client.inspect(roots[0].handleId);
```

### MCP Server
Location: `mcp-server/`

Model Context Protocol adapter for AI agent integration:

```bash
cd mcp-server
npm install
npm run build
npm start
```

Exposes tools:
- `game_list_roots()` - Get root objects
- `game_inspect_object(handleId)` - Inspect an object
- `game_clear_handles()` - Clear handle registry

### Web Client
Location: `client-web/`

React-based browser UI for exploring game state:

```bash
cd client-web
npm install
npm run dev
```

Open http://localhost:3000 in your browser.

## Architecture

```
┌─────────────────────────────────────────────────────────────────┐
│                        Game Process (Unity)                      │
│  ┌───────────────────────────────────────────────────────────┐  │
│  │                      Viewer Mod                            │  │
│  │  ┌─────────────┐  ┌─────────────┐  ┌─────────────────┐   │  │
│  │  │ HTTP Server │  │   Handle    │  │ Object Inspector │   │  │
│  │  │ (port 5000) │  │  Registry   │  │  (Reflection)    │   │  │
│  │  └─────────────┘  └─────────────┘  └─────────────────┘   │  │
│  └───────────────────────────────────────────────────────────┘  │
└─────────────────────────────────────────────────────────────────┘
                              ▲
                              │ HTTP+JSON
                              ▼
         ┌────────────────────┴────────────────────┐
         │                                          │
    ┌────▼────┐                              ┌─────▼─────┐
    │   Web   │                              │    MCP    │
    │ Client  │                              │  Server   │
    └─────────┘                              └─────┬─────┘
         │                                         │
         ▼                                         ▼
    ┌─────────┐                              ┌───────────┐
    │ Browser │                              │  VS Code  │
    │  (You)  │                              │  (Agent)  │
    └─────────┘                              └───────────┘
```

## Handle System

Objects are not serialized deeply. Instead:
- **Primitives** (bool, int, string, enums) are returned inline
- **References** are returned as opaque handles (GUIDs)

To inspect a reference, call `/api/inspect` with its handle ID.

This avoids:
- Infinite recursion from circular references
- Unbounded response sizes
- Deep serialization of the entire game state

## Configuration

### Server Port
Default: `5000`

### Test Configuration
Create `scripts/test-config.json` to use a different URL:

```json
{
  "baseUrl": "http://localhost:5001"
}
```

## Development

### Prerequisites
- .NET SDK (for building the mod)
- Node.js 18+ (for clients and tests)
- Rogue Trader with UnityModManager

### Project Structure
```
viewer-mod/
├── mod/                    # C# Unity mod
│   ├── src/
│   │   ├── Entry.cs
│   │   ├── ViewerBehaviour.cs
│   │   ├── Server/
│   │   ├── State/
│   │   └── Models/
│   ├── ViewerMod.csproj
│   └── Info.json
├── sdk/                    # Shared TypeScript SDK
├── mcp-server/            # MCP server for AI agents
├── client-web/            # React web client
├── scripts/               # Build and test scripts
└── spec/                  # Design specifications
```

## Troubleshooting

### Server not reachable
1. Check Unity log for mod errors
2. Verify mod is loaded in UnityModManager
3. Confirm a save is loaded (roots require game state)

### No roots returned
- A save must be loaded for Game.Instance to exist

### Image extraction fails
- Object must be a Texture2D or Sprite
- Texture must be readable (some are GPU-only)

## License

MIT
