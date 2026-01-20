# Decision Notes / Counterfactuals

> [!note]
> This file is optional reference material. It exists to record decisions and constraints, not to drive Phase 1 scope.

This file captures final decisions and (briefly) what would cause us to change them.

## Core Philosophy
This is a **systems debugging tool**: prioritize completeness, transparency, and correctness over convenience, safety, or premature optimization. See DESIGN_PHILOSOPHY_NOTES.md for detailed philosophy.

## Safety / Read-only
- "Read-only" is defined as **no destructive or user-visible** game state changes.
- Non-destructive side effects are acceptable (cache warming, encoding textures to bytes, generating intermediate state per request).

## Networking / Security
- Start with **localhost**.
- LAN access is out of Phase 1 scope.
- No authentication planned initially (private LAN, single primary user).

## Handles / Memory
**Final Decision:**
- Single global registry (no sessions)
- Strong references (GUIDs → objects)
- Client-driven cleanup via `clear_handles()`
- Acceptable for clients to leak handles; game restart clears everything

## Introspection Philosophy
**Final Decision:**
- **Visibility:** Public + Internal members (no private)
- **Completeness:** Return all members; no filtering, truncation, or limits
- **Cleaning:** Done client-side; server returns raw complete data
- **No server-side filters:** Clients handle all filtering/prettification

## No Artificial Limits
**Final Decision:**
- No collection size limits (return entire collection)
- No member count limits (return all public + internal)
- No truncation or defensive filtering
- If client requests data, return all of it
- Collections: return count + all element handles

Rationale: This is a systems tool. Truncating data would be like `ps` limiting output to 100 processes.


## Collections
**Final Decision:**
- Return entire collection in single response
- Include count + array of all element handles
- No paging, limits, or lazy loading in v1
- If collection mutates between requests, return current state


## Schemas / Compatibility (Phase 2)
**Decision:**
- Schemas deferred until Phase 2 (after core inspection proven)
- Will include game version tagging (opaque string, documentation only)
- Type identity: assembly name + full type name
- No compatibility policy yet


## Binary Extraction
**Final Decision:**
- Added `GET /api/image/{handleId}` endpoint.
- Server performs on-the-fly conversion (e.g., `ImageConversion.EncodeToPNG`) on the main thread.
- Returns standard HTTP binary response with correct Content-Type.
- Essential for the "Icon Extraction" workflow.

## Schema Storage (Phase 2)
**Final Decision:**
- **Repository is Source of Truth**: Schemas live in the `viewer-mod` repo (or adjacent standard folder).
- **Mod is Persistence-Agnostic**: The game mod intentionally doesn't know about schema files. It just exposes objects.
- **MCP Server as Writer**: The MCP server (having disk access) is responsible for writing schema files to the repo.
- **Web Client as Viewer**: The web client can read schemas (via import or potentially fetching from MCP/local server later) but doesn't write directly.

## Shared Code Code
**Final Decision:**
- Use a simple shared source folder for the TypeScript SDK.
- No complex npm workspaces/monorepo setup initially.
- Copy or symlink `sdk` folder between `mcp-server` and `client-web` (or standard relative import if structure allows).

## Performance
**Decision:**
- Implement efficiently by default
- Profile and optimize only when measured as necessary
- Occasional stutter acceptable in v1
- Start with all reflection on main thread; move off-thread only if profiling shows need

Philosophy: "Program efficiently with room to iterate, don't program defensively for latency"


## Implementation Phases
**Phase 1 (Current):** Core inspection with HTTP+JSON
- Server mod with 3 endpoints
- MCP server for agent access
- Web client with tree viewer
- Raw reflection views only

**Phase 2 (Future):** Schema & Viewers
- Schema definitions
- Custom viewer HTML files
- AI schema authoring

**Phase 3 (Future):** Live Debugging
- WebSocket push/subscriptions
- Reactive value observation
- Reuses Phase 1 object/handle model
