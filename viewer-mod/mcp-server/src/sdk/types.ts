/**
 * TypeScript types matching the server wire format.
 */

/**
 * A root entry point into the game's object graph.
 */
export interface RootEntry {
  name: string;
  handleId: string;
  type: string;
  assemblyName: string;
}

/**
 * Response from the /api/inspect endpoint.
 */
export interface InspectResponse {
  handleId: string;
  type: string;
  assemblyName: string;
  value: unknown;
  members: MemberInfo[];
  collectionInfo: CollectionInfo | null;
}

/**
 * Information about a member (field or property) of an object.
 */
export interface MemberInfo {
  name: string;
  type: string;
  assemblyName: string;
  isPrimitive: boolean;
  handleId: string | null;
  value: unknown;
}

/**
 * Information about a collection object.
 */
export interface CollectionInfo {
  isCollection: boolean;
  count: number;
  elementType: string;
  elements: CollectionElement[];
}

/**
 * An element within a collection.
 */
export interface CollectionElement {
  index: number;
  handleId: string | null;
  type: string;
  value: unknown;
}

/**
 * Error response from the server.
 */
export interface ErrorResponse {
  error: string;
  handleId?: string;
  path?: string;
}

/**
 * Response from clearing handles.
 */
export interface ClearHandlesResponse {
  cleared: boolean;
}
