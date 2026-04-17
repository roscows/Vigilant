import { type FormEvent, useEffect, useMemo, useRef, useState } from 'react';
import ForceGraph2D, { type ForceGraphMethods, type NodeObject } from 'react-force-graph-2d';
import type { EntityGraphEdge, EntityGraphNode } from '../api/types';
import { useAlertsStore } from '../store/alertsStore';
import { useGraphStore } from '../store/graphStore';

interface AmlGraphViewerProps {
  onLoadOverview: () => Promise<void>;
  onSearchIban: (iban: string) => Promise<void>;
  highlightedAccountIds?: string[];
}

type GraphNode = EntityGraphNode & { name: string; val: number };
type GraphLink = EntityGraphEdge & { source: string; target: string; label: string };

const nodeColors: Record<string, string> = {
  Client: '#3b82f6',
  Account: '#10b981',
  Transaction: '#f59e0b',
  IpAddress: '#8b5cf6',
  Device: '#ec4899',
};

const edgeColors: Record<string, string> = {
  OWNS: 'rgba(59, 130, 246, 0.55)',
  SENT: 'rgba(16, 185, 129, 0.7)',
  RECEIVED_BY: 'rgba(245, 158, 11, 0.66)',
  EXECUTED_FROM_IP: 'rgba(139, 92, 246, 0.48)',
  EXECUTED_ON_DEVICE: 'rgba(236, 72, 153, 0.48)',
  USES_DEVICE: 'rgba(236, 72, 153, 0.36)',
};

export function AmlGraphViewer({ onLoadOverview, onSearchIban, highlightedAccountIds = [] }: AmlGraphViewerProps) {
  const graphRef = useRef<ForceGraphMethods<GraphNode, GraphLink> | undefined>(undefined);
  const frameRef = useRef<HTMLDivElement | null>(null);
  const nodes = useGraphStore((state) => state.nodes);
  const links = useGraphStore((state) => state.links);
  const loading = useGraphStore((state) => state.loading);
  const highlightedNodeKeys = useGraphStore((state) => state.highlightedNodeKeys);
  const isRealtimeConnected = useAlertsStore((state) => state.isRealtimeConnected);
  const [hoveredNode, setHoveredNode] = useState<GraphNode | null>(null);
  const [ibanSearch, setIbanSearch] = useState('');
  const [size, setSize] = useState({ width: 900, height: 640 });

  useEffect(() => {
    if (!frameRef.current) {
      return;
    }

    const observer = new ResizeObserver(([entry]) => {
      setSize({
        width: Math.max(360, Math.floor(entry.contentRect.width)),
        height: Math.max(520, Math.floor(entry.contentRect.height)),
      });
    });

    observer.observe(frameRef.current);
    return () => observer.disconnect();
  }, []);

  useEffect(() => {
    if (nodes.length > 0) {
      window.setTimeout(() => graphRef.current?.zoomToFit(650, 80), 250);
    }
  }, [nodes.length]);

  const graphData = useMemo(() => ({
    nodes: nodes.map(toGraphNode),
    links: links.map((link) => ({ ...link, source: link.sourceId, target: link.targetId, label: link.type })),
  }), [links, nodes]);

  const highlightedKeys = useMemo(
    () => new Set([...highlightedNodeKeys, ...highlightedAccountIds].map((key) => key.toLowerCase())),
    [highlightedAccountIds, highlightedNodeKeys],
  );
  const hasHighlight = highlightedKeys.size > 0;

  const submitIbanSearch = (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();

    const trimmedIban = ibanSearch.trim();
    if (trimmedIban.length === 0) {
      return;
    }

    void onSearchIban(trimmedIban);
  };

  return (
    <section className="graph-workspace">
      <header className="graph-toolbar glass-panel">
        <div>
          <p className="eyebrow">Investigation Graph</p>
          <h1>Entity Network</h1>
        </div>
        <div className="graph-toolbar__actions">
          <form className="iban-search" onSubmit={submitIbanSearch}>
            <label htmlFor="iban-search">Focus on IBAN</label>
            <div>
              <input
                id="iban-search"
                type="search"
                value={ibanSearch}
                placeholder="RS35105008123456789"
                autoComplete="off"
                spellCheck={false}
                onChange={(event) => setIbanSearch(event.target.value)}
              />
              <button type="submit" disabled={loading || ibanSearch.trim().length === 0}>
                Search
              </button>
            </div>
          </form>
          <span className="connection-status">
            <i className={isRealtimeConnected ? 'status-dot status-dot--online' : 'status-dot status-dot--offline'} />
            {isRealtimeConnected ? 'Live' : 'Disconnected'}
          </span>
          <button type="button" onClick={() => void onLoadOverview()} disabled={loading}>
            Reset View
          </button>
          <button className="secondary-button" type="button" onClick={() => graphRef.current?.zoomToFit(500, 80)}>
            Fit View
          </button>
        </div>
      </header>

      <div className="graph-canvas-card glass-panel" ref={frameRef}>
        {loading ? <div className="graph-loading">Loading graph intelligence...</div> : null}
        {graphData.nodes.length === 0 && !loading ? (
          <div className="graph-empty-state">
            <strong>No graph loaded</strong>
            <span>Click Reset View, open an alert, or focus on an account IBAN.</span>
          </div>
        ) : null}
        {graphData.nodes.length > 0 ? (
          <ForceGraph2D<GraphNode, GraphLink>
            ref={graphRef}
            graphData={graphData}
            width={size.width}
            height={size.height}
            backgroundColor="rgba(15, 17, 23, 0)"
            nodeId="id"
            nodeVal="val"
            nodeLabel={(node) => buildTooltip(node as GraphNode)}
            nodeCanvasObject={(node, ctx, scale) => paintNode(node, ctx, scale, highlightedKeys, hasHighlight)}
            nodePointerAreaPaint={paintNodePointerArea}
            onNodeHover={(node) => setHoveredNode(node as GraphNode | null)}
            linkSource="source"
            linkTarget="target"
            linkLabel={(link) => (link as GraphLink).label}
            linkColor={(link) => getLinkColor(link as GraphLink)}
            linkWidth={(link) => ((link as GraphLink).type === 'SENT' ? 1.8 : 1.15)}
            linkDirectionalArrowLength={4}
            linkDirectionalArrowRelPos={1}
            linkDirectionalParticles={(link) => ((link as GraphLink).type === 'SENT' ? 2 : 0)}
            linkDirectionalParticleSpeed={0.007}
            minZoom={0.28}
            maxZoom={5}
            cooldownTicks={90}
            d3VelocityDecay={0.28}
            enableNodeDrag
            enablePanInteraction
            enableZoomInteraction
          />
        ) : null}
        {hoveredNode ? <NodeTooltip node={hoveredNode} /> : null}
      </div>
    </section>
  );
}

function toGraphNode(node: EntityGraphNode): GraphNode {
  return {
    ...node,
    name: getNodeName(node),
    val: getNodeRadius(node),
  };
}

function paintNode(
  node: NodeObject<GraphNode>,
  ctx: CanvasRenderingContext2D,
  globalScale: number,
  highlightedKeys: Set<string>,
  hasHighlight: boolean,
) {
  const graphNode = node as GraphNode;
  const x = node.x ?? 0;
  const y = node.y ?? 0;
  const radius = getNodeRadius(graphNode);
  const isHighlighted = !hasHighlight || nodeMatchesKeys(graphNode, highlightedKeys);
  const alpha = isHighlighted ? 1 : 0.15;
  const color = nodeColors[graphNode.label] ?? '#94a3b8';

  ctx.save();
  ctx.globalAlpha = alpha;
  ctx.beginPath();
  ctx.arc(x, y, radius, 0, Math.PI * 2);
  ctx.fillStyle = color;
  ctx.shadowBlur = isHighlighted ? 22 : 0;
  ctx.shadowColor = color;
  ctx.fill();

  if (hasHighlight && isHighlighted) {
    ctx.beginPath();
    ctx.arc(x, y, radius + 4, 0, Math.PI * 2);
    ctx.lineWidth = Math.max(1.4, 2.5 / globalScale);
    ctx.strokeStyle = '#ffffff';
    ctx.shadowBlur = 14;
    ctx.shadowColor = '#ffffff';
    ctx.stroke();
  }

  if (globalScale > 0.8 && isHighlighted) {
    ctx.shadowBlur = 0;
    ctx.font = `600 ${Math.max(4, 11 / globalScale)}px Aptos, Segoe UI, sans-serif`;
    ctx.textAlign = 'center';
    ctx.textBaseline = 'top';
    ctx.fillStyle = 'rgba(248, 250, 252, 0.86)';
    ctx.fillText(truncate(graphNode.name, 22), x, y + radius + 4 / globalScale);
  }

  ctx.restore();
}

function paintNodePointerArea(node: NodeObject<GraphNode>, color: string, ctx: CanvasRenderingContext2D) {
  const graphNode = node as GraphNode;
  ctx.fillStyle = color;
  ctx.beginPath();
  ctx.arc(node.x ?? 0, node.y ?? 0, getNodeRadius(graphNode) + 5, 0, Math.PI * 2);
  ctx.fill();
}

function getNodeRadius(node: EntityGraphNode): number {
  const riskScore = Number(node.properties.RiskScore ?? 0);
  if (Number.isFinite(riskScore) && riskScore > 0) {
    return Math.min(20, Math.max(6, 6 + (riskScore / 100) * 14));
  }

  return node.label === 'Transaction' ? 6 : 8;
}

function getLinkColor(link: GraphLink): string {
  return edgeColors[link.type] ?? 'rgba(148, 163, 184, 0.35)';
}

function nodeMatchesKeys(node: GraphNode, keys: Set<string>): boolean {
  return getNodeKeys(node).some((key) => keys.has(key.toLowerCase()));
}

function getNodeKeys(node: GraphNode): string[] {
  const props = node.properties;
  return [
    node.id,
    stringValue(props.Id),
    stringValue(props.IBAN),
    stringValue(props.Address),
    stringValue(props.DeviceId),
    stringValue(props.BrowserFingerprint),
  ].filter((value): value is string => Boolean(value));
}

function NodeTooltip({ node }: { node: GraphNode }) {
  return (
    <aside className="node-tooltip">
      <span>{node.label}</span>
      <strong>{node.name}</strong>
      <p>{buildTooltip(node)}</p>
    </aside>
  );
}

function buildTooltip(node: GraphNode): string {
  const props = node.properties;
  if (node.label === 'Client') {
    return `${stringValue(props.Name) ?? node.name} | Risk ${numberValue(props.RiskScore).toFixed(0)}${props.IsPep === true ? ' | PEP' : ''}`;
  }

  if (node.label === 'Account') {
    return `${stringValue(props.IBAN) ?? node.name} | Balance ${formatNumber(props.Balance)} | ${stringValue(props.CountryCode) ?? 'N/A'}`;
  }

  if (node.label === 'Transaction') {
    return `${formatNumber(props.Amount)} ${stringValue(props.Currency) ?? ''} | ${stringValue(props.Timestamp) ?? 'No timestamp'}`;
  }

  if (node.label === 'IpAddress') {
    return `${stringValue(props.Address) ?? node.name} | ${stringValue(props.CountryCode) ?? 'N/A'}`;
  }

  if (node.label === 'Device') {
    return `${stringValue(props.DeviceId) ?? node.name} | ${stringValue(props.BrowserFingerprint) ?? 'No fingerprint'}`;
  }

  return node.name;
}

function getNodeName(node: EntityGraphNode): string {
  const props = node.properties;
  return stringValue(props.Name) ??
    stringValue(props.IBAN) ??
    stringValue(props.Id) ??
    stringValue(props.Address) ??
    stringValue(props.DeviceId) ??
    node.id;
}

function stringValue(value: unknown): string | undefined {
  return typeof value === 'string' && value.length > 0 ? value : undefined;
}

function numberValue(value: unknown): number {
  return typeof value === 'number' && Number.isFinite(value) ? value : 0;
}

function formatNumber(value: unknown): string {
  return numberValue(value).toLocaleString('en-US', { maximumFractionDigits: 0 });
}

function truncate(value: string, length: number): string {
  return value.length > length ? `${value.slice(0, length - 1)}...` : value;
}
