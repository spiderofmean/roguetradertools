# Automation Guide: Development & Testing

## Purpose
Enable an AI agent to build and test the Viewer Mod with minimal human oversight.

## Key Automation Requirements

### 1. Contract-First Development
Define **exact wire formats** before implementation to enable parallel development and validation.

#### Wire Format Specifications (JSON)

**`POST /api/roots` Response:**
```json
[
  {
    "name": "Game.Instance",
    "handleId": "00000000-0000-0000-0000-000000000001",
    "type": "Kingmaker.Game",
    "assemblyName": "Assembly-CSharp"
  },
  {
    "name": "ResourcesLibrary.Instance",
    "handleId": "00000000-0000-0000-0000-000000000002",
    "type": "Kingmaker.Blueprints.ResourcesLibrary",
    "assemblyName": "Code"
  }
]
```

**`POST /api/inspect` Request:**
```json
{
  "handleId": "00000000-0000-0000-0000-000000000001"
}
```

**`POST /api/inspect` Response (Object):**
```json
{
  "handleId": "00000000-0000-0000-0000-000000000001",
  "type": "Kingmaker.Game",
  "assemblyName": "Assembly-CSharp",
  "value": "Kingmaker.Game",
  "members": [
    {
      "name": "Player",
      "type": "Kingmaker.Player",
      "assemblyName": "Assembly-CSharp",
      "isPrimitive": false,
      "handleId": "00000000-0000-0000-0000-000000000003",
      "value": "Kingmaker.Player"
    },
    {
      "name": "CurrentlyLoadedAreaPart",
      "type": "Kingmaker.EntitySystem.Persistence.SaveManager",
      "assemblyName": "Code",
      "isPrimitive": false,
      "handleId": "00000000-0000-0000-0000-000000000004",
      "value": null
    },
    {
      "name": "IsModeActive",
      "type": "System.Boolean",
      "assemblyName": "mscorlib",
      "isPrimitive": true,
      "handleId": null,
      "value": true
    },
    {
      "name": "Version",
      "type": "System.String",
      "assemblyName": "mscorlib",
      "isPrimitive": true,
      "handleId": null,
      "value": "1.2.3"
    }
  ],
  "collectionInfo": null
}
```

**`POST /api/inspect` Response (Collection):**
```json
{
  "handleId": "00000000-0000-0000-0000-000000000005",
  "type": "System.Collections.Generic.List`1[Kingmaker.Blueprints.BlueprintItem]",
  "assemblyName": "mscorlib",
  "value": "System.Collections.Generic.List`1[Kingmaker.Blueprints.BlueprintItem]",
  "members": [],
  "collectionInfo": {
    "isCollection": true,
    "count": 3,
    "elementType": "Kingmaker.Blueprints.BlueprintItem",
    "elements": [
      {
        "index": 0,
        "handleId": "00000000-0000-0000-0000-000000000006",
        "type": "Kingmaker.Blueprints.BlueprintItem",
        "value": "Lasgun"
      },
      {
        "index": 1,
        "handleId": "00000000-0000-0000-0000-000000000007",
        "type": "Kingmaker.Blueprints.BlueprintItem",
        "value": "PlasmaGun"
      },
      {
        "index": 2,
        "handleId": "00000000-0000-0000-0000-000000000008",
        "type": "Kingmaker.Blueprints.BlueprintItem",
        "value": "Bolter"
      }
    ]
  }
}
```

**`GET /api/image/{handleId}` Response:**
- HTTP 200 OK
- Content-Type: `image/png`
- Body: Binary PNG data (starts with `89 50 4E 47` magic bytes)

**Error Response (All Endpoints):**
```json
{
  "error": "Handle not found",
  "handleId": "00000000-0000-0000-0000-000000000999"
}
```

### 2. Mock Server (Optional)

If you want to build the clients without the game running, add a tiny Node mock server that:
- Listens on the same port as the real server (default `5000`)
- Serves responses that match the wire formats above
- Serves a small PNG from `GET /api/image/{handleId}`

Keep it minimal: it exists to unblock client development, not to simulate the whole game.

### 3. Build & Deploy (Minimal)

Automate only what you repeat:

- Build the mod: `dotnet build mod/ViewerMod.csproj -c Release`
- Copy the output DLL and `mod/Info.json` into your UnityModManager mods folder

### 4. Tests (Source of Truth)

Use the three-script flow in [TEST_PLAN.md](TEST_PLAN.md). That plan defines what “done” means.

Tests assume the server is on `http://localhost:5000`.

#### Repo-Local Test Config (No Env Vars)

Only create this file if you intentionally run the server somewhere else.

Create `scripts/test-config.json` (untracked / local):

```json
{ "baseUrl": "http://localhost:5001" }
```

### 5. Debugging When Tests Fail

- First check the Unity log for the server exception.
- Then re-run the smallest failing test and iterate.
