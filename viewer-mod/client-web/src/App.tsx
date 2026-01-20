import { useState, useEffect, useCallback } from 'react';
import { GameClient, RootEntry, InspectResponse } from './sdk';
import { ObjectTree } from './components/ObjectTree';
import { InspectView } from './components/InspectView';

const client = new GameClient({ baseUrl: '' }); // Use relative URLs with Vite proxy

export default function App() {
  const [roots, setRoots] = useState<RootEntry[]>([]);
  const [selectedHandleId, setSelectedHandleId] = useState<string | null>(null);
  const [inspectData, setInspectData] = useState<InspectResponse | null>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [cache, setCache] = useState<Map<string, InspectResponse>>(new Map());

  const loadRoots = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const data = await client.getRoots();
      setRoots(data);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to load roots');
    } finally {
      setLoading(false);
    }
  }, []);

  const inspectObject = useCallback(async (handleId: string) => {
    // Check cache first
    const cached = cache.get(handleId);
    if (cached) {
      setInspectData(cached);
      setSelectedHandleId(handleId);
      return cached;
    }

    setLoading(true);
    setError(null);
    try {
      const data = await client.inspect(handleId);
      // Update cache
      setCache(prev => new Map(prev).set(handleId, data));
      setInspectData(data);
      setSelectedHandleId(handleId);
      return data;
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to inspect object');
      return null;
    } finally {
      setLoading(false);
    }
  }, [cache]);

  const clearHandles = useCallback(async () => {
    try {
      await client.clearHandles();
      setRoots([]);
      setSelectedHandleId(null);
      setInspectData(null);
      setCache(new Map());
      setError(null);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to clear handles');
    }
  }, []);

  // Load roots on mount
  useEffect(() => {
    loadRoots();
  }, [loadRoots]);

  return (
    <div className="app">
      <header className="header">
        <h1>Viewer Mod - Game State Explorer</h1>
        <div className="header-actions">
          <button className="btn btn-primary" onClick={loadRoots} disabled={loading}>
            Refresh Roots
          </button>
          <button className="btn btn-danger" onClick={clearHandles}>
            Clear Handles
          </button>
        </div>
      </header>

      <main className="main-content">
        <aside className="sidebar">
          <div className="sidebar-header">Object Graph</div>
          <div className="tree-container">
            {loading && roots.length === 0 ? (
              <div className="loading">Loading...</div>
            ) : error && roots.length === 0 ? (
              <div className="error">{error}</div>
            ) : roots.length === 0 ? (
              <div className="empty">No roots found. Is the game running?</div>
            ) : (
              <ObjectTree
                roots={roots}
                selectedHandleId={selectedHandleId}
                onSelect={inspectObject}
                cache={cache}
                onInspect={inspectObject}
              />
            )}
          </div>
        </aside>

        <section className="detail-panel">
          {loading && !inspectData ? (
            <div className="loading">Loading...</div>
          ) : error && !inspectData ? (
            <div className="error">{error}</div>
          ) : inspectData ? (
            <InspectView
              data={inspectData}
              onNavigate={inspectObject}
              client={client}
            />
          ) : (
            <div className="empty">Select an object to inspect</div>
          )}
        </section>
      </main>
    </div>
  );
}
