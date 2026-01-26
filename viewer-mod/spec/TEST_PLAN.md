# Test Plan — Lean

This plan keeps testing to the smallest set of checks that prove the system works end-to-end.

## What “Passing” Means

All test scripts follow the same conventions:
- Exit code `0` = pass, `1` = fail
- Minimal output on success (ideally a single line)
- On failure: a single actionable reason
- No test framework (plain Node)

## Tests

We intentionally keep this to **three** scripts. Each one proves everything “below” it.

### 1) Static sanity: `node scripts/test-01-static.js`

**Runs without the game.**

Checks:
- `mod/Info.json` parses and includes `EntryMethod`
- Repo contains the expected top-level components (mod + clients)
- TypeScript (if present) compiles with `tsc --noEmit`

Pass condition:
- Script completes with no errors.

What this proves:
- The repo is internally consistent (basic build-time failures are caught early).

### 2) Live connectivity: `node scripts/test-02-connectivity.js`

**Requires the game running with the mod loaded and a save open.**

Checks (all must return valid JSON):
- `POST /api/roots` returns a non-empty array
- `POST /api/inspect` on the first root returns an object with a `members` array
- `POST /api/handles/clear` returns `{ "cleared": true }`

Pass condition:
- All three calls succeed.

What this proves:
- The server is reachable and the minimum API contract is implemented.

### 3) Live end-to-end: `node scripts/test-03-integration.js`

**Requires the game running with the mod loaded and a save open.**

Checks:
- Traverse at least 3 object levels by repeatedly choosing a non-primitive member and inspecting it
- Validate collections: `collectionInfo.count === collectionInfo.elements.length`
- Validate image extraction:
	- Find a `Texture2D` or `Sprite`
	- `GET /api/image/{handleId}` returns bytes with a valid PNG header (`89 50 4E 47`)
	- Save one image to `test-output/image.png`

Pass condition:
- Traversal works, collections are internally consistent, and the image endpoint returns valid bytes.

What this proves:
- Handles + reflection work for real game objects.
- Binary extraction works end-to-end.

## Failure Triage

| Test | Likely Cause | Next Step |
|:---|:---|:---|
| 01 | Build/layout issue | Fix `Info.json`, TypeScript, or missing folders |
| 02 | Server not running or exception | Check the Unity log; verify base URL/port |
| 03 | Handle/image bug | Reproduce with a single handle; then fix encoding/handle lookup |

