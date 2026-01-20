import {
  RootEntry,
  InspectResponse,
  ClearHandlesResponse,
  ErrorResponse,
} from './types';

/**
 * Configuration options for the GameClient.
 */
export interface GameClientConfig {
  baseUrl?: string;
  timeout?: number;
}

/**
 * Client for communicating with the Viewer Mod game server.
 */
export class GameClient {
  private readonly baseUrl: string;
  private readonly timeout: number;

  constructor(config: GameClientConfig = {}) {
    this.baseUrl = config.baseUrl ?? 'http://localhost:5000';
    this.timeout = config.timeout ?? 30000;
  }

  /**
   * Gets the list of root entry points into the game's object graph.
   */
  async getRoots(): Promise<RootEntry[]> {
    const response = await this.post<RootEntry[]>('/api/roots', {});
    return response;
  }

  /**
   * Inspects an object by its handle ID.
   */
  async inspect(handleId: string): Promise<InspectResponse> {
    const response = await this.post<InspectResponse>('/api/inspect', { handleId });
    return response;
  }

  /**
   * Clears all handles from the server's registry.
   */
  async clearHandles(): Promise<void> {
    await this.post<ClearHandlesResponse>('/api/handles/clear', {});
  }

  /**
   * Gets the URL for fetching an image by handle ID.
   */
  getImageUrl(handleId: string): string {
    return `${this.baseUrl}/api/image/${handleId}`;
  }

  /**
   * Fetches image bytes for a texture or sprite handle.
   */
  async getImage(handleId: string): Promise<ArrayBuffer> {
    const url = this.getImageUrl(handleId);
    const response = await this.fetchRequest(url, {
      method: 'GET',
    });

    if (!response.ok) {
      const errorText = await response.text();
      throw new Error(`Failed to fetch image: ${response.status} ${errorText}`);
    }

    return response.arrayBuffer();
  }

  private async post<T>(path: string, body: unknown): Promise<T> {
    const url = `${this.baseUrl}${path}`;
    
    const response = await this.fetchRequest(url, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
      },
      body: JSON.stringify(body),
    });

    if (!response.ok) {
      const errorBody = await response.text();
      let errorMessage: string;
      try {
        const errorJson = JSON.parse(errorBody) as ErrorResponse;
        errorMessage = errorJson.error;
      } catch {
        errorMessage = errorBody || `HTTP ${response.status}`;
      }
      throw new Error(`API error: ${errorMessage}`);
    }

    return response.json() as Promise<T>;
  }

  private async fetchRequest(url: string, options: RequestInit): Promise<Response> {
    const controller = new AbortController();
    const timeoutId = setTimeout(() => controller.abort(), this.timeout);

    try {
      const response = await fetch(url, {
        ...options,
        signal: controller.signal,
      });
      return response;
    } finally {
      clearTimeout(timeoutId);
    }
  }
}

export * from './types';
