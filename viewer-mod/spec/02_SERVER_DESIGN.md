# Server Design Specification (Draft)

## Component: Object Handle Manager
The core of the server is the `ObjectHandleManager`. It bridges the gap between the .NET memory space and the external JSON-based clients.

### Responsibilities
1.  **Registry**: stores a mapping `HandleId -> object`.
  *   Keys are opaque IDs (UUIDs are fine).
  *   Values are **strong references** to in-memory objects.
2.  **Dereferencing**: Retrieval of an object by UUID.
3.  **Reflection**: Given an object (by UUID), reflect over its fields/properties and return a "Wire Representation".
4.  **Root Enumeration**: Provides entry points into the graph.
  *   Provide an explicit list of root objects (known singletons / services) discovered during implementation.

### Handle Model
- **Single global registry** (no sessions)
- Handle IDs are GUIDs (`System.Guid`)
- **Strong references**: objects remain alive until cleared
- **Cleanup is explicit**: clients call `POST /api/handles/clear`
- No automatic expiry, leases, or reference counting

## API Specification (Phase 1: Synchronous Pull)

The server will expose a local HTTP+JSON API. This is strictly a **Pull** model.

Key property: endpoints should be *stateless from the client's perspective*, even if they build temporary intermediate state internally.

Correctness direction:
- Prefer returning complete inspection results ("inspect the state") over defensive truncation.
- Avoid server-side "safety valve" limits that would intentionally prevent inspection.

### Endpoints

#### 1. `POST /api/roots`
Returns a list of known entry points into the object graph.
*   **Request**: `{}` (empty or omitted body)
*   **Response**: `[{ "name": "Game.Instance", "handleId": "uuid-1", "type": "Kingmaker.Game", "assemblyName": "Assembly-CSharp" }, ...]`

Notes:
- Returns a small explicit list of root objects.

#### 2. `POST /api/inspect`
Reflects on a specific object handle.
*   **Request**: `{ "handleId": "uuid-1" }`
*   **Response**: 
    ```json
    {
      "handleId": "uuid-1",
      "type": "Kingmaker.Game",
      "assemblyName": "Assembly-CSharp",
      "value": "Kingmaker.Game",
      "members": [
        { 
          "name": "Player", 
          "type": "Kingmaker.Player", 
          "isPrimitive": false,
          "handleId": "uuid-2",
          "value": "Kingmaker.Player"
        },
        { 
          "name": "Time", 
          "type": "System.TimeSpan", 
          "isPrimitive": true,
          "value": "12:00:00"
        }
      ],
      "collectionInfo": null
    }
    ```

Notes:
- **Visibility**: Return public + non-public members except private (i.e., include internal/protected/protected-internal).
- **No filtering**: Return the complete member set; clients handle filtering/prettification.
- **Primitives**: `bool`, `int`, `long`, `float`, `double`, `string`, enums (as string names), `null`
- **Collections**: If object is a collection, `collectionInfo` includes `{ "isCollection": true, "count": N, "elementType": "...", "elements": [...] }`
- **Collections return all elements**: No paging, limits, or truncation.

#### 3. `GET /api/image/{handleId}`
Returns the binary image data for a texture or sprite.
*   **Request**: `GET /api/image/uuid-1`
*   **Response**: Binary image data (PNG/JPG)
    *   Content-Type: `image/png` (or appropriate type)
*   **Behavior**:
    *   Main thread resolves handle.
    *   If object is `Texture2D`, `Sprite`, or convertible type:
    *   Calls `ImageConversion.EncodeToPNG()` (or similar Unity API) on main thread.
    *   Returns bytes.
    *   If not an image or error, returns 404 or 400.

#### 4. `POST /api/handles/clear`
Clears all handles in the global registry.
*   **Request**: `{}` (empty or omitted body)
*   **Response**: `{ "cleared": true }`

Notes:
- Deferred: `resolve_path` endpoint (clients chain `inspect` calls).

## Reflection Strategy
*   **Depth**: Shallow - only immediate members returned
*   **Visibility**: Public + Internal/Protected (exclude private)
*   **References**: Non-primitive types get Handle IDs (reuse existing if already tracked)
*   **Collections**: Materialize entire collection and return all elements as handles
  - No paging, truncation, or limits
  - Return count + array of all element handles
  - If collection mutates between requests, return current state
*   **No filtering**: Return complete member set; clients handle filtering

## Nuances & Tradeoffs

### Threading
*   **HTTP Listener**: Runs on its own threads (System.Net.HttpListener)
*   **Request Queue**: Incoming requests queued for processing
*   **Main Thread**: Process queue in Unity `Update()` loop via MonoBehaviour
*   **v1 Strategy**: Run reflection on the main thread for simplicity.

### Serialization Loops
By using Handles for *everything* non-primitive, we avoid infinite recursion loops in JSON serialization.

### Performance
Performance is not a primary constraint for v1; occasional stutter during deep inspection is acceptable.

## Deferred (Not Phase 1)

- Push/subscriptions (WebSocket)
- Convenience endpoints like path resolution
- Schema/viewer authoring
