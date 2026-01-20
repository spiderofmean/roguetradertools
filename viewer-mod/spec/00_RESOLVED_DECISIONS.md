# Resolved Architectural Decisions

Based on user feedback and inspection of existing codebase, the following decisions are locked in:

## Technology Stack

### Deployment Model
- **Mod Framework:** UnityModManager (UMM) style with `Info.json` manifest
- **Entry Point:** Static method specified in `Info.json` (`EntryMethod`)
- **Game Process:** .NET Framework 4.7.2 running in Unity
- **Behavior Pattern:** `MonoBehaviour` subclass with `Update()` loop for frame-based operations

### Available Assemblies (from game)
- ✅ **Newtonsoft.Json** - Available in game's Managed folder
- ✅ **UnityEngine** + modules (Core, ImageConversion, Input, etc.)
- ✅ **System.Net** classes (standard .NET Framework 4.7.2)
- ✅ Game-specific assemblies (Owlcat.Runtime.Core, Kingmaker.*)

### HTTP Server
- **Decision:** Use `System.Net.HttpListener` (available in .NET 4.7.2)
- **Binding:** `http://localhost:<port>/` (configurable port, default: 5000)
- **Threading:** HttpListener runs its own threads; queue work to Unity main thread via `Update()`

### JSON Serialization
- **Library:** Newtonsoft.Json (already referenced by game)
- **Style:** Standard JSON with consistent type representations

## Core Architecture Decisions

### Handle Management
- **Scope:** Single global registry (no session isolation)
- **Type:** Strong references (GC keeps objects alive)
- **Cleanup:** Explicit client-driven via `POST /api/handles/clear`
- **ID Format:** GUIDs (System.Guid)

### Object Inspection
- **Visibility:** Public + internal/protected (exclude private)
- **Depth:** One level at a time (return handles for referenced objects)
- **Collections:** Return entire collection (all elements) as array of handles
- **Null Handling:** Explicit in JSON (`"value": null`, `"handleId": null`)

### API Endpoints (Phase 1)

#### Essential
1. `POST /api/roots` - Get entry point objects
2. `POST /api/inspect` - Inspect object by handle
3. `POST /api/handles/clear` - Clear all handles

#### Deferred
- ❌ `resolve_path` (clients chain inspects)
- ❌ `query` primitives (not needed yet)
- ❌ Session management endpoints (single global registry)
- ❌ WebSocket/push (Phase 2)

### Roots Strategy
- Hardcoded list of 3-5 key entry points
- Likely candidates (to be verified):
  - `Game.Instance`
  - Unity scene roots
  - Known singleton services

### Wire Format

#### Primitive Types
Types returned inline (not as handles):
- `bool`, `int`, `long`, `float`, `double`, `string`
- `null`
- Enums (as string names)

#### Reference Types
Types returned as handles:
- All classes/objects (unless primitive-like, see above)
- Arrays/Lists/Collections (return as handle to collection object)
- Unity types (Texture2D, GameObject, etc.)

#### Response Structure
```json
{
  "handleId": "guid-uuid",
  "type": "Full.Type.Name",
  "assemblyName": "AssemblyName",
  "value": "ToString() representation",
  "members": [
    {
      "name": "FieldName",
      "type": "Field.Type.Name", 
      "isPrimitive": true,
      "value": <primitive-value>
    },
    {
      "name": "RefField",
      "type": "Reference.Type.Name",
      "isPrimitive": false,
      "handleId": "guid-uuid-2",
      "value": "ToString() representation"
    }
  ],
  "collectionInfo": {
    "isCollection": true,
    "count": 100,
    "elementType": "Element.Type.Name",
    "elements": [
      { "handleId": "guid-uuid-3", "value": "..." },
      // ... all elements
    ]
  }
}
```

## Client Architecture

### Web Client
- **Framework:** React + Vite (simple, fast iteration)
- **State:** Local cache of inspected objects keyed by handleId
- **UI:** Tree/graph explorer with expandable references
- **Viewers:** HTML files on disk with registry system (Phase 2)

### MCP Server
- **Runtime:** Node.js (TypeScript)
- **Protocol:** stdio to VS Code, HTTP to game server
- **Tools:**
  - `game_list_roots()`
  - `game_inspect_object(handleId)`
  - `game_clear_handles()`

### Shared SDK
- TypeScript library for game server communication
- Used by both web client and MCP server
- Handles JSON parsing, error handling, response caching

## Implementation Phases

### Phase 1: Core Inspection (Current Focus)
**Goal:** Navigate object graph from both MCP and web UI

**Server (C# mod):**
1. HTTP listener on localhost
2. Handle registry (GUID -> object)
3. Reflection engine (public + internal members)
4. Three endpoints: roots, inspect, clear
5. Main thread dispatcher for Unity calls

**MCP Server:**
1. Basic MCP tools calling game server HTTP API
2. Session management (track active handles)
3. Error handling and response formatting

**Web Client:**
1. Simple React tree viewer
2. API client (shared SDK)
3. Expand/collapse object references
4. Raw reflection view (no schemas yet)

**Success Criteria:**
- Agent can navigate from root to specific object
- Human can explore object graph in browser
- Both can retrieve member values and follow references

### Phase 2: Schema & Viewers (Future)
- Schema definitions (JSON files)
- Schema-driven rendering
- AI schema authoring tools
- Viewer registration and selection
- Custom viewer HTML files

### Phase 3: Advanced Features (Future)
- WebSocket for push notifications
- Subscriptions to value changes
- Query/path resolution (if needed)
- Performance optimizations (if measured as necessary)

## Non-Functional Requirements

### Correctness
- Return complete data (no truncation)
- Return current state (no “snapshot” guarantees)

### Simplicity
- Minimal server; smart clients
- No pre-emptive limits
