import { useState, useEffect } from 'react';
import { InspectResponse, MemberInfo, CollectionElement, GameClient } from '../sdk';

interface InspectViewProps {
  data: InspectResponse;
  onNavigate: (handleId: string) => void;
  client: GameClient;
}

export function InspectView({ data, onNavigate, client }: InspectViewProps) {
  const shortType = data.type.split('.').pop() ?? data.type;
  const isTexture = data.type.includes('Texture2D') || data.type.includes('Sprite');

  return (
    <>
      <div className="detail-header">
        <h2>{shortType}</h2>
        <div className="type">{data.type}</div>
        <div className="type" style={{ color: '#666' }}>
          Assembly: {data.assemblyName} | Handle: {data.handleId}
        </div>
      </div>
      <div className="detail-content">
        {isTexture && (
          <ImagePreview handleId={data.handleId} client={client} />
        )}

        {data.members.length > 0 && (
          <MembersTable members={data.members} onNavigate={onNavigate} />
        )}

        {data.collectionInfo && (
          <CollectionView
            info={data.collectionInfo}
            onNavigate={onNavigate}
          />
        )}
      </div>
    </>
  );
}

interface MembersTableProps {
  members: MemberInfo[];
  onNavigate: (handleId: string) => void;
}

function MembersTable({ members, onNavigate }: MembersTableProps) {
  return (
    <table className="members-table">
      <thead>
        <tr>
          <th>Name</th>
          <th>Type</th>
          <th>Value</th>
        </tr>
      </thead>
      <tbody>
        {members.map((member) => (
          <tr key={member.name}>
            <td className="member-name">{member.name}</td>
            <td className="member-type">{member.type.split('.').pop()}</td>
            <td>
              <MemberValue member={member} onNavigate={onNavigate} />
            </td>
          </tr>
        ))}
      </tbody>
    </table>
  );
}

interface MemberValueProps {
  member: MemberInfo;
  onNavigate: (handleId: string) => void;
}

function MemberValue({ member, onNavigate }: MemberValueProps) {
  if (member.value === null) {
    return <span className="member-value null">null</span>;
  }

  if (member.isPrimitive) {
    const displayValue = typeof member.value === 'string'
      ? `"${member.value}"`
      : String(member.value);
    return <span className="member-value primitive">{displayValue}</span>;
  }

  if (member.handleId) {
    return (
      <span
        className="member-value reference"
        onClick={() => onNavigate(member.handleId!)}
      >
        {String(member.value)} →
      </span>
    );
  }

  return <span className="member-value null">null</span>;
}

interface CollectionViewProps {
  info: {
    count: number;
    elementType: string;
    elements: CollectionElement[];
  };
  onNavigate: (handleId: string) => void;
}

function CollectionView({ info, onNavigate }: CollectionViewProps) {
  return (
    <div className="collection-info">
      <div className="collection-header">
        Collection: {info.count} elements of {info.elementType.split('.').pop()}
      </div>
      <div className="collection-elements">
        <table className="members-table">
          <thead>
            <tr>
              <th>Index</th>
              <th>Type</th>
              <th>Value</th>
            </tr>
          </thead>
          <tbody>
            {info.elements.map((elem) => (
              <tr key={elem.index}>
                <td>[{elem.index}]</td>
                <td className="member-type">{elem.type.split('.').pop()}</td>
                <td>
                  {elem.handleId ? (
                    <span
                      className="member-value reference"
                      onClick={() => onNavigate(elem.handleId!)}
                    >
                      {String(elem.value)} →
                    </span>
                  ) : elem.value === null ? (
                    <span className="member-value null">null</span>
                  ) : (
                    <span className="member-value primitive">
                      {String(elem.value)}
                    </span>
                  )}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  );
}

interface ImagePreviewProps {
  handleId: string;
  client: GameClient;
}

function ImagePreview({ handleId, client }: ImagePreviewProps) {
  const [imageUrl, setImageUrl] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    let cancelled = false;

    async function loadImage() {
      setLoading(true);
      setError(null);
      try {
        const bytes = await client.getImage(handleId);
        if (cancelled) return;

        const blob = new Blob([bytes], { type: 'image/png' });
        const url = URL.createObjectURL(blob);
        setImageUrl(url);
      } catch (err) {
        if (cancelled) return;
        setError(err instanceof Error ? err.message : 'Failed to load image');
      } finally {
        if (!cancelled) {
          setLoading(false);
        }
      }
    }

    loadImage();

    return () => {
      cancelled = true;
      if (imageUrl) {
        URL.revokeObjectURL(imageUrl);
      }
    };
  }, [handleId, client]);

  return (
    <div className="image-preview">
      <h3>Image Preview</h3>
      {loading && <div className="loading">Loading image...</div>}
      {error && <div className="error">{error}</div>}
      {imageUrl && <img src={imageUrl} alt="Texture preview" />}
    </div>
  );
}
