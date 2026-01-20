# Client & MCP Design Specification (Draft)

## 1. Web-Tech Client (The Viewer)

A browser-based Single Page Application (SPA) facilitating human exploration of the game state.

### Tech Stack
*   **Framework**: React + Vite (fast iteration, simple setup)
*   **State Management**: Local cache keyed by `handleId` with inspected objects
*   **Language**: TypeScript for type safety

### Key Features
1.  **Object Explorer**: Tree-view or Graph-view of the data. 
    *   Clicking a reference field requests `inspect` for that handle from the server.
    *   Default philosophy: prefer full/raw server responses and do most filtering/pretty-printing client-side.
2.  **Schema Derivation**: 
    *   The client can aggregate info from multiple instances of the same type to propose a "Schema". 
    *   *Example*: "We've seen 10 `ItemEntity` objects. They always have `m_Blueprint` and `Count`. Let's create a simplified view showing just those."
3.  **Custom Views** (Phase 2): 
    *   Multiple viewers per type (many:1), selected by user or context
    *   Viewers as HTML files on disk with registry system
    *   Support for viewer composition
    *   Example: Texture2D viewer fetching and rendering image data

Collections:
- Server returns all elements; client handles display/pagination if needed

## 2. Model Context Protocol (MCP) Server

An adapter bridging the AI Agent (VS Code) to the running Game Server.

### Role
Allows an AI agent to "see" into the game while writing code or debugging. The agent can verify assumptions about the object structure against the live instance.

Direction: keep the MCP layer thin; it forwards a small set of primitives and the agent navigates by chaining `inspect` calls.

### Tools Exposed to AI
The MCP Server will expose the following tools:

*   `game_list_roots()`: Get entry point objects
*   `game_inspect_object(handle_id)`: Get members of an object by handle
*   `game_clear_handles()`: Clear all handles from registry

Deferred tools:
*   `game_resolve_path()`: clients can chain `inspect` calls

### Workflow Example
1.  User asks AI: "How do I extract the Icon from a Blueprint?"
2.  AI calls `game_list_roots()` → gets root objects including game instance
3.  AI calls `game_inspect_object(root_handle)` → discovers `BlueprintLoader` or similar
4.  AI navigates by repeatedly calling `inspect` on handles until finding `BlueprintItem`
5.  AI inspects `BlueprintItem` to discover `m_Icon` field structure
6.  AI writes C# code using discovered field names and types

## 3. Integration & Shared Logic
Both the Web Client and MCP Server share a common "SDK" or library for communicating with the Game Server.

*   **SDK Layer**: TypeScript library (usable in both browser and Node).
    *   Methods: `getRoots()`, `inspect(handleId, options)`, and optional helpers like `resolvePath(...)`.
    *   Owns request bounds defaults and response parsing.

## 4. Two Client Modes (Important)
This project likely supports two related but distinct product modes:

1) **Explorer / Asset Viewer**: pull-based exploration, schema/view generation, agent-scriptable.
2) **Live Debugger**: event hooks + WebSocket push for reactive views.

They should share the same object/handle model and as much wire format as practical.

## Scope Notes

Phase 1 focuses on:
- roots → inspect → expand references
- basic rendering of primitives/objects/collections
- image fetch/display for textures/sprites
