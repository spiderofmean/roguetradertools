# Next Actions - Implementation Readiness

## Summary
All architectural decisions have been resolved. The design philosophy is clear: build a **complete, transparent systems tool** with minimal server, smart clients, and no artificial limits. Ready to proceed with implementation.

## Key Philosophy Takeaways
1. **Systems tool mindset** - prioritize completeness over convenience
2. **No artificial limits** - return all requested data
3. **Public + Internal visibility** - comprehensive introspection
4. **Multi-client required** - web UI + MCP from day one
5. **Simple choices** - defer complexity until proven necessary

## Technology Stack Confirmed
- ✅ UnityModManager deployment (like existing extractor mod)
- ✅ Newtonsoft.Json available in game
- ✅ System.Net.HttpListener for HTTP server
- ✅ .NET Framework 4.7.2
- ✅ MonoBehaviour with Update() for Unity integration

## Ready to Implement: Phase 1 - Core Inspection

### What to Build

#### 1. Server Mod (C#)
**Location:** `viewer-mod/mod/`

**Components:**
- **Entry.cs** - Mod entry point (like Dumper.cs)
- **ViewerBehaviour.cs** - MonoBehaviour with Update() loop
- **Server/HttpServer.cs** - HttpListener wrapper
- **Server/RequestDispatcher.cs** - Queue requests for main thread
- **State/HandleRegistry.cs** - GUID → object map
- **State/ObjectInspector.cs** - Reflection engine (public + internal)
- **Models/WireFormat.cs** - Response DTOs
- **ViewerMod.csproj** - Project file (copy from BlueprintDumper)
- **Info.json** - UMM manifest

**API Endpoints:**

| Method | Path | Purpose |
|:--|:--|:--|
| POST | `/api/roots` | List entry points |
| POST | `/api/inspect` | Inspect an object by `handleId` |
| GET | `/api/image/{handleId}` | Fetch image bytes for Texture/Sprite |
| POST | `/api/handles/clear` | Clear all handles |

#### 2. Shared SDK (TypeScript)
**Location:** `viewer-mod/sdk/` (Source)
**Usage:** Copied/Symlinked to both `client-web/src/sdk/` and `mcp-server/src/sdk/`

**Files:**
- **client.ts** - HTTP client for game server
- **types.ts** - TypeScript types matching wire format

**Exports:**
```typescript
class GameClient {
  getRoots(): Promise<Root[]>
  inspect(handleId: string): Promise<InspectResponse>
  clearHandles(): Promise<void>
}
```

#### 3. MCP Server (TypeScript)
**Location:** `viewer-mod/mcp-server/`

**Files:**
- **index.ts** - MCP server implementation
- **package.json** - Dependencies (@modelcontextprotocol/sdk)

**Tools:**
```typescript
game_list_roots()
game_inspect_object(handleId: string)
game_clear_handles()
```

#### 4. Web Client (React)
**Location:** `viewer-mod/client-web/`

**Components:**
- **App.tsx** - Main app shell
- **components/ObjectTree.tsx** - Recursive tree view
- **components/InspectView.tsx** - Display object details
- **stores/objectStore.ts** - Cache inspected objects
- **api/client.ts** - Import shared SDK

### Implementation Order (Practical)

1. Implement the server endpoints (`roots`, `inspect`, `handles/clear`, `image`)
2. Make [TEST_PLAN.md](TEST_PLAN.md) pass in order: 01 → 02 → 03
3. Wire up MCP tools (thin adapter)
4. Build the web tree viewer

## Automation Notes

Automation is useful, but keep it small:
- A build + deploy script
- The three test scripts in [TEST_PLAN.md](TEST_PLAN.md)
- Logging that points to the failure cause

## Files Updated This Session

Created:
- [spec/DESIGN_PHILOSOPHY_NOTES.md](viewer-mod/spec/DESIGN_PHILOSOPHY_NOTES.md) - Philosophy extracted from your feedback
- [spec/00_RESOLVED_DECISIONS.md](viewer-mod/spec/00_RESOLVED_DECISIONS.md) - All architectural decisions locked in

These should inform all future implementation work.
