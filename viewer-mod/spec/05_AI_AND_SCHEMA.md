# AI Integration & Schema Derivation (Draft)

## Vision
The tool is not just a viewer, but a platform for an AI to *learn* the game's structure and *teach* the viewer how to display it.

**Note**: Schema and viewer system is Phase 2. Phase 1 focuses on core inspection with raw reflection views.

## The Schema System

### 1. The Challenge
The game has thousands of types. Hardcoding views for each is impossible.
Raw reflection is accurate but noisy (too many members; lots of internal state).

### 2. The Solution: Schemas + Viewers
Distinguish two concepts:

- **Schema**: declarative, data-only description of how to *present* a type/instance.
- **Viewer**: an implementation that can render an object (one viewer may be schema-driven; another may be bespoke code).

We expect *many viewers per in-game type*, with selection driven by context (e.g., “inventory view” vs “debug raw view”).

Initial approach: schema-first (fast iteration, easy for AI to propose).

## Schema Storage & Portability
Schemas and views are intended to be reusable across sessions and users.

- **Storage Authority**: The Git Repository is the Source of Truth.
- **Management**: The MCP Server manages reading/writing schema files in the repository. The Game Mod does **not** read/write schema files.
- **Web Client**: Reads schemas and applies them; saving schemas happens via repo writes.

#### Schema Definition (Draft)
```json
{
  "targetType": "Kingmaker.Blueprints.BlueprintItem",
  "gameVersion": "<opaque game build string>",
  "version": "1.0",
  "display": {
    "titleField": "m_DisplayName", // Use this field for the header
    "iconField": "m_Icon",         // Use this field for the icon
    "groups": [
      {
        "name": "Stats",
        "fields": ["m_Cost", "m_Weight"]
      },
      {
        "name": "Capabilities",
        "fields": ["m_Abilities", "m_Actions"]
      }
    ]
  }
}
```

Schema rules (keep it simple):
- `targetType` is the fully qualified type name
- `gameVersion` is an opaque tag for humans (grouping / sanity checks)
- Field names are raw member names from inspection (no expression language in Phase 2)

## AI Workflow: The "Mapper" Agent

We envision an AI Agent (accessed via MCP) acting as a "Cartographer".

### Workflow (Phase 2)
1.  **Exploration**: Agent traverses object graph using `game_inspect_object`
2.  **Analysis**: Agent identifies types needing cleaner views
3.  **Generation**: Agent generates Schema JSON, prioritizing readable fields
4.  **Deployment**: Schema saved to `viewer-mod/client-web/data/schemas/` (version controlled)
5.  **Refinement**: User reviews in Web Client; agent iterates on schema as needed

## Phase 1 Reminder

Phase 1 is still raw inspection:
- No schema system required
- No AI writing files required

## Dynamic UI Generation
The Web Client will feature a **SchemaRenderer** component.
*   **Input**: `ObjectData` (start handle) + `Schema` (definition).
*   **Logic**: 
    *   If a Schema exists for `ObjectData.Type`, use it.
    *   Otherwise, fall back to "Raw Reflection" view.

## Roadmap for AI Features
1.  **Passive**: AI reads state to answer questions.
2.  **Active Definition**: AI writes Schema files to clear up the UI.
3.  **Code Generation (Optional)**: AI proposes bespoke viewer code, but keep this behind explicit review due to blast radius.

## What We Explicitly Don’t Do (Phase 2)

- No schema expression language
- No schema compatibility policy beyond tagging with `gameVersion`
- No viewer composition system
- No automatic “discover every type and write schemas for all of them”
