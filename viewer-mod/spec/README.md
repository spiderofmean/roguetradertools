# Viewer Mod Specification

This folder specifies a **live, read-only game state viewer** for Warhammer 40,000: Rogue Trader.

The design is deliberately simple:
- A minimal in-process server (handles + reflection + HTTP)
- Two clients from day one (Web UI + MCP)
- No server-side truncation or “helpful” filtering

## Read Order

1. [01_SYSTEM_OVERVIEW.md](01_SYSTEM_OVERVIEW.md) — what we’re building
2. [DESIGN_PHILOSOPHY_NOTES.md](DESIGN_PHILOSOPHY_NOTES.md) — rules of engagement
3. [02_SERVER_DESIGN.md](02_SERVER_DESIGN.md) — server contract and behavior
4. [03_CLIENT_AND_MCP_DESIGN.md](03_CLIENT_AND_MCP_DESIGN.md) — clients and shared SDK
5. [TEST_PLAN.md](TEST_PLAN.md) — how we prove it works
6. [AUTOMATION_GUIDE.md](AUTOMATION_GUIDE.md) — wire formats + minimal automation
7. [00_NEXT_ACTIONS.md](00_NEXT_ACTIONS.md) — build order checklist

Optional / reference:
- [00_RESOLVED_DECISIONS.md](00_RESOLVED_DECISIONS.md)
- [90_DECISION_NOTES.md](90_DECISION_NOTES.md)
- [04_PROJECT_STRUCTURE.md](04_PROJECT_STRUCTURE.md)
- [05_AI_AND_SCHEMA.md](05_AI_AND_SCHEMA.md) (Phase 2)

## Core Idea

Expose the running game’s object graph as:
- **Primitives inline** (numbers/strings/bools/null/enums)
- **Everything else by handle** (opaque GUIDs clients can inspect)

Clients traverse by repeatedly calling `inspect(handleId)`.

## Architecture

```mermaid
flowchart LR
  subgraph Game[Game Process (Unity)]
    Mod[Viewer Mod Server]
    Handles[(Handle Registry)]
    Mod --> Handles
  end

  Mod <--> |HTTP+JSON| Web[Web Client]
  Mod <--> |HTTP+JSON| MCP[MCP Server]
  MCP <--> |MCP stdio| VS[VS Code / Agent]
  Web --> Browser[Browser]
```

## Phase 1 Done When

- The three scripts in [TEST_PLAN.md](TEST_PLAN.md) pass against a live game session
- Web UI can browse roots → inspect → expand references
- MCP tools can browse the same graph
- `GET /api/image/{handleId}` returns valid image bytes for at least one real texture/sprite

## Design Choices (Short)

- **Handles instead of deep serialization**: keeps responses bounded and avoids cycles.
- **No server-side filtering**: server returns raw truth; clients decide how to present it.
- **HTTP+JSON**: smallest working transport for Phase 1.

## Getting Unstuck

- Start from [TEST_PLAN.md](TEST_PLAN.md) and make one script pass at a time.
- When the live tests fail, check the Unity log for the server exception first.
