# System Overview: Live Game State Viewer (Draft)

## Vision
Build a live, invasive (in-process), read-only debugger for Rogue Trader game state.

The primary pain point this addresses is iteration speed: explore live objects (including textures/icons) without rebuild/redeploy/save-load cycles.

## Goals
- Allow humans and agents to traverse live game state as an object graph.
- Provide stable handles to in-memory objects, avoiding deep serialization.
- Support multiple clients concurrently (web UI + agent tooling).
- Keep the mod minimal: introspection + transport + handle registry.

## Non-goals (Initial)
- Mutating game state.
- A full remote debugger (breakpoints, stepping, IL rewriting, etc.).
- Guaranteed long-term handle stability across scene loads / save loads.
- Perfect, universal “type schemas” for all game types.

## Read-only Semantics
"Read-only" means clients must not be able to cause destructive or user-visible changes to game state.

Some non-destructive side effects are acceptable if they support inspection (e.g., loading a cache, encoding a texture to egress image bytes).

## Design Philosophy
This is a **systems tool** for comprehensive debugging, not an end-user application:
- **Completeness over convenience**: Never truncate, limit, or heuristically filter data
- **Transparency over safety**: Show all public + internal members
- **Correctness over performance**: Implement efficiently, optimize only when measured as necessary
- **Full information**: When client requests data, return all of it
- **Simple implementation**: Defer complexity until demonstrated need
- **Multi-client required**: Both human (web UI) and agent (MCP) from day one

## Deployment Assumptions
- Start with localhost-only for simplicity.
- Plan for LAN access later (debugging from another machine).
- No authentication is planned initially (private LAN, single primary user).
- UnityModManager deployment (same as extractor mod).
- .NET Framework 4.7.2, Newtonsoft.Json available in game assemblies.

## Core Concepts

### 1. In-Game Server (The "Mod")
A lightweight introspection server running within the Unity game process.
*   **Responsibility**: 
    *   Maintains a managed map of `HandleID -> Object Reference`.
    *   Exposes a local API (initially HTTP+JSON; push is optional later).
    *   Performs bounded reflection/introspection on tracked objects.
    *   Provides access to "Root" objects (explicitly registered roots; optional discovery).
*   **State Management**: 
    *   Uses a "Handle" system to avoid deep serialization. 
    *   Clients request details for a specific object Handle.
    *   Requests return immediate primitive values and Handles for referenced objects.
*   **Phasing**:
    *   **Phase 1 (Inspection)**: Pull-based HTTP+JSON. This is the current implementation focus.
    *   **Phase 2 (Observation)**: Push-based subscriptions (WebSocket), explicitly deferred until Phase 1 is complete and proven.

### 2. The Client Layer
Multiple clients should be able to connect to the server.
*   **Web-Tech Client**: A browser-based application for human exploration, schema visualization, and UI construction.
*   **MCP Server**: A Model Context Protocol adapter that allows AI agents (e.g., inside VS Code) to query game state using tools.

### 3. Schema & Metadata
The system treats the game state as a graph of generic objects. 
*   **Discovery**: Schemas are derived at runtime via reflection.
*   **Augmentation**: AI can propose human-friendly schemas/viewers based on observed instances.
*   **Mapping**: A clean interface between in-game types and their viewers (many viewers per type).

Direction:
- Prefer returning raw/complete inspection data and doing most filtering/pretty-printing on the client.
- Schemas/views should be reusable artifacts and should include game-version compatibility metadata.

Correctness direction:
- Prefer inspecting the state as-is (avoid truncation/omission intended to "protect" the game).

Data model direction:
- Treat objects, lists, and maps as first-class concepts in the server's representation.

## Architecture Diagram (Conceptual)

```mermaid
graph TD
    subgraph "Game Process (Unity)"
        GS[Game State / Objects]
        Mod[Viewer Mod Server]
        HandleTable[Handle Map <UUID, StrongRef>]
        
        Mod -->|Reflects| GS
        Mod -->|Manages| HandleTable
    end

    subgraph "External Host (Localhost)"
        MCP[MCP Server]
        Web[Web Client]
    end

    subgraph "AI / Dev Environment"
        VSCode[VS Code + Copilot]
        Browser[Web Browser]
    end

    Web <-->|HTTP+JSON (localhost)| Mod
    MCP <-->|HTTP+JSON (localhost)| Mod
    VSCode <-->|MCP (stdio)| MCP
    Browser --> Web
```
