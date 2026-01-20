import { useState, useCallback } from 'react';
import { RootEntry, InspectResponse } from '../sdk';

interface ObjectTreeProps {
  roots: RootEntry[];
  selectedHandleId: string | null;
  onSelect: (handleId: string) => Promise<InspectResponse | null>;
  cache: Map<string, InspectResponse>;
  onInspect: (handleId: string) => Promise<InspectResponse | null>;
}

interface TreeNodeState {
  expanded: boolean;
  children: TreeNodeData[];
  loaded: boolean;
}

interface TreeNodeData {
  handleId: string;
  name: string;
  type: string;
  value: unknown;
  isPrimitive: boolean;
}

export function ObjectTree({
  roots,
  selectedHandleId,
  onSelect,
  cache,
  onInspect,
}: ObjectTreeProps) {
  return (
    <div className="object-tree">
      {roots.map((root) => (
        <TreeNode
          key={root.handleId}
          handleId={root.handleId}
          name={root.name}
          type={root.type}
          value={null}
          isPrimitive={false}
          depth={0}
          selectedHandleId={selectedHandleId}
          onSelect={onSelect}
          cache={cache}
          onInspect={onInspect}
        />
      ))}
    </div>
  );
}

interface TreeNodeProps {
  handleId: string;
  name: string;
  type: string;
  value: unknown;
  isPrimitive: boolean;
  depth: number;
  selectedHandleId: string | null;
  onSelect: (handleId: string) => Promise<InspectResponse | null>;
  cache: Map<string, InspectResponse>;
  onInspect: (handleId: string) => Promise<InspectResponse | null>;
}

function TreeNode({
  handleId,
  name,
  type,
  depth,
  selectedHandleId,
  onSelect,
  cache,
  onInspect,
}: TreeNodeProps) {
  const [state, setState] = useState<TreeNodeState>({
    expanded: false,
    children: [],
    loaded: false,
  });

  const isSelected = selectedHandleId === handleId;

  const toggle = useCallback(async (e: React.MouseEvent) => {
    e.stopPropagation();

    if (state.expanded) {
      setState((prev) => ({ ...prev, expanded: false }));
      return;
    }

    // Load children if not loaded
    if (!state.loaded) {
      const data = cache.get(handleId) ?? (await onInspect(handleId));
      if (data) {
        const children: TreeNodeData[] = [];

        // Add members
        for (const member of data.members) {
          if (!member.isPrimitive && member.handleId) {
            children.push({
              handleId: member.handleId,
              name: member.name,
              type: member.type,
              value: member.value,
              isPrimitive: false,
            });
          }
        }

        // Add collection elements
        if (data.collectionInfo) {
          for (const elem of data.collectionInfo.elements) {
            if (elem.handleId) {
              children.push({
                handleId: elem.handleId,
                name: `[${elem.index}]`,
                type: elem.type,
                value: elem.value,
                isPrimitive: false,
              });
            }
          }
        }

        setState({
          expanded: true,
          children,
          loaded: true,
        });
      }
    } else {
      setState((prev) => ({ ...prev, expanded: true }));
    }
  }, [state.expanded, state.loaded, handleId, cache, onInspect]);

  const handleClick = useCallback(() => {
    onSelect(handleId);
  }, [onSelect, handleId]);

  const shortType = type.split('.').pop() ?? type;

  return (
    <div className="tree-node" style={{ paddingLeft: depth * 12 }}>
      <div
        className={`tree-node-content ${isSelected ? 'selected' : ''}`}
        onClick={handleClick}
      >
        <span
          className={`tree-toggle ${state.expanded ? 'expanded' : ''}`}
          onClick={toggle}
        >
          ▶
        </span>
        <span className="tree-label">
          <strong>{name}</strong>
          <span style={{ color: '#888', marginLeft: 8, fontSize: '0.8rem' }}>
            {shortType}
          </span>
        </span>
      </div>
      {state.expanded && state.children.length > 0 && (
        <div className="tree-children">
          {state.children.map((child) => (
            <TreeNode
              key={child.handleId}
              handleId={child.handleId}
              name={child.name}
              type={child.type}
              value={child.value}
              isPrimitive={child.isPrimitive}
              depth={depth + 1}
              selectedHandleId={selectedHandleId}
              onSelect={onSelect}
              cache={cache}
              onInspect={onInspect}
            />
          ))}
        </div>
      )}
    </div>
  );
}
