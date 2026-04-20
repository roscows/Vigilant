import { type FormEvent, useEffect, useMemo, useRef, useState } from 'react';
import ForceGraph2D, { type ForceGraphMethods, type NodeObject } from 'react-force-graph-2d';
import type { EntityGraphEdge, EntityGraphNode } from '../api/types';
import { useAlertsStore, type AccountAlertFocus } from '../store/alertsStore';
import { useGraphStore } from '../store/graphStore';

interface AmlGraphViewerProps {
  onLoadOverview: () => Promise<void>;
  onSearchIban: (iban: string) => Promise<void>;
  highlightedAccountIds?: string[];
}

type GraphNode = EntityGraphNode & { name: string; val: number };
type GraphLink = EntityGraphEdge & { source: string; target: string; label: string };
type KnownNodeLabel = 'Client' | 'Account' | 'Transaction' | 'IpAddress' | 'Device';
interface GraphLookup {
  nodesByKey: Map<string, GraphNode>;
  ownerByAccountKey: Map<string, GraphNode>;
  outboundAccountByTransactionKey: Map<string, GraphNode>;
  inboundAccountByTransactionKey: Map<string, GraphNode>;
}

const nodeColors: Record<KnownNodeLabel, string> = {
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

const nodeLegend = [
  { label: 'Client', color: nodeColors.Client },
  { label: 'Account', color: nodeColors.Account },
  { label: 'Transaction', color: nodeColors.Transaction },
  { label: 'IP', color: nodeColors.IpAddress },
  { label: 'Device', color: nodeColors.Device },
];

export function AmlGraphViewer({ onLoadOverview, onSearchIban, highlightedAccountIds = [] }: AmlGraphViewerProps) {
  const graphRef = useRef<ForceGraphMethods<GraphNode, GraphLink> | undefined>(undefined);
  const frameRef = useRef<HTMLDivElement | null>(null);
  const nodes = useGraphStore((state) => state.nodes);
  const links = useGraphStore((state) => state.links);
  const loading = useGraphStore((state) => state.loading);
  const highlightedNodeKeys = useGraphStore((state) => state.highlightedNodeKeys);
  const setAccountFocus = useAlertsStore((state) => state.setAccountFocus);
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
  const graphLookup = useMemo(() => buildGraphLookup(graphData.nodes, links), [graphData.nodes, links]);

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

  const focusNodeAlerts = (node: GraphNode) => {
    const focus = buildAccountAlertFocus(node, graphData.nodes, links);
    if (!focus) {
      return;
    }

    setAccountFocus(focus);
    setIbanSearch(focus.ibanQuery);

    const firstIban = focus.ibanQuery.split(',')[0]?.trim();
    if (firstIban) {
      void onSearchIban(firstIban);
    }
  };

  return (
    <section className="graph-workspace">
      <header className="graph-toolbar glass-panel">
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
          <div className="graph-toolbar__buttons">
            <button type="button" onClick={() => void onLoadOverview()} disabled={loading}>
              Reset View
            </button>
            <button className="secondary-button" type="button" onClick={() => graphRef.current?.zoomToFit(500, 80)}>
              Fit View
            </button>
          </div>
        </div>
        <ul className="node-legend" aria-label="Graph node legend">
          {nodeLegend.map((item) => (
            <li key={item.label}>
              <i style={{ backgroundColor: item.color }} />
              <span><strong>{item.label}</strong></span>
            </li>
          ))}
        </ul>
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
            nodeLabel={(node) => buildTooltip(node as GraphNode, graphLookup)}
            nodeCanvasObject={(node, ctx, scale) => paintNode(node, ctx, scale, highlightedKeys, hasHighlight)}
            nodePointerAreaPaint={paintNodePointerArea}
            onNodeHover={(node) => setHoveredNode(node as GraphNode | null)}
            onNodeClick={(node) => focusNodeAlerts(node as GraphNode)}
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
        {hoveredNode ? <NodeTooltip node={hoveredNode} graphLookup={graphLookup} /> : null}
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
  const color = getNodeColor(graphNode);

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
  ctx.arc(node.x ?? 0, node.y ?? 0, getNodeRadius(graphNode) + 9, 0, Math.PI * 2);
  ctx.fill();
}

function getNodeRadius(node: EntityGraphNode): number {
  const label = getPrimaryLabel(node);
  const minimumRadius = getMinimumNodeRadius(label);
  const riskScore = Number(node.properties.RiskScore ?? 0);
  if (Number.isFinite(riskScore) && riskScore > 0) {
    return Math.min(22, Math.max(minimumRadius, minimumRadius + (riskScore / 100) * 8));
  }

  return minimumRadius;
}

function getMinimumNodeRadius(label: KnownNodeLabel | undefined): number {
  if (label === 'Client') {
    return 16;
  }

  if (label === 'Account') {
    return 13;
  }

  if (label === 'Transaction') {
    return 10;
  }

  if (label === 'IpAddress' || label === 'Device') {
    return 11;
  }

  return 11;
}

function getLinkColor(link: GraphLink): string {
  return edgeColors[link.type] ?? 'rgba(148, 163, 184, 0.35)';
}

function getNodeColor(node: EntityGraphNode): string {
  const label = getPrimaryLabel(node);
  return label ? nodeColors[label] : '#94a3b8';
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

function NodeTooltip({ node, graphLookup }: { node: GraphNode; graphLookup: GraphLookup }) {
  return (
    <aside className="node-tooltip">
      <span>{getPrimaryLabel(node) ?? node.label ?? 'Entity'}</span>
      <strong>{node.name}</strong>
      <p>{buildTooltip(node, graphLookup)}</p>
    </aside>
  );
}

function buildTooltip(node: GraphNode, graphLookup?: GraphLookup): string {
  const props = node.properties;
  const label = getPrimaryLabel(node);

  if (label === 'Client') {
    return `${node.name} | Risk ${numberValue(props.RiskScore).toFixed(0)}${props.IsPep === true ? ' | PEP' : ''}`;
  }

  if (label === 'Account') {
    const ownerName = graphLookup ? getAccountOwnerName(node, graphLookup) : undefined;
    return `${ownerName ? `Owner ${ownerName} | ` : ''}${stringValue(props.IBAN) ?? node.name} | Balance ${formatNumber(props.Balance)} | ${stringValue(props.CountryCode) ?? 'N/A'}`;
  }

  if (label === 'Transaction') {
    const sourceAccount = graphLookup ? getRelatedAccountName(node, graphLookup.outboundAccountByTransactionKey) : undefined;
    const destinationAccount = graphLookup ? getRelatedAccountName(node, graphLookup.inboundAccountByTransactionKey) : undefined;
    const route = sourceAccount || destinationAccount ? ` | ${sourceAccount ?? 'Unknown source'} -> ${destinationAccount ?? 'Unknown destination'}` : '';
    return `${node.name} | ${formatNumber(props.Amount)} ${stringValue(props.Currency) ?? ''} | ${stringValue(props.Timestamp) ?? 'No timestamp'}${route}`;
  }

  if (label === 'IpAddress') {
    return `${node.name} | ${stringValue(props.Address) ?? node.name} | ${stringValue(props.CountryCode) ?? 'N/A'}`;
  }

  if (label === 'Device') {
    return `${node.name} | ${stringValue(props.DeviceId) ?? node.name} | ${stringValue(props.BrowserFingerprint) ?? 'No fingerprint'}`;
  }

  return buildGenericTooltip(node);
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

function buildAccountAlertFocus(
  node: GraphNode,
  graphNodes: GraphNode[],
  graphLinks: EntityGraphEdge[],
): AccountAlertFocus | null {
  const label = getPrimaryLabel(node);
  if (label === 'Account') {
    const accountId = getBusinessNodeId(node);
    const iban = stringValue(node.properties.IBAN);
    if (!accountId || !iban) {
      return null;
    }

    return {
      ibanQuery: iban,
      accountIds: [accountId],
      sourceLabel: iban,
    };
  }

  if (label !== 'Client') {
    return null;
  }

  const clientKeys = getNodeKeys(node);
  if (clientKeys.length === 0) {
    return null;
  }
  const clientKeySet = new Set(clientKeys);

  const accountsById = new Map<string, GraphNode>();
  graphNodes
    .filter((candidate) => getPrimaryLabel(candidate) === 'Account')
    .forEach((account) => {
      getNodeKeys(account).forEach((key) => accountsById.set(key, account));
    });

  const ownedAccounts = graphLinks
    .filter((link) => link.type === 'OWNS' && clientKeySet.has(link.sourceId))
    .map((link) => accountsById.get(link.targetId))
    .filter((account): account is GraphNode => Boolean(account));

  const accountIds = ownedAccounts
    .map(getBusinessNodeId)
    .filter((accountId): accountId is string => Boolean(accountId));

  const ibans = ownedAccounts
    .map((account) => stringValue(account.properties.IBAN))
    .filter((iban): iban is string => Boolean(iban));

  if (accountIds.length === 0 || ibans.length === 0) {
    return null;
  }

  return {
    ibanQuery: ibans.join(', '),
    accountIds,
    sourceLabel: stringValue(node.properties.Name) ?? node.name,
  };
}

function getBusinessNodeId(node: EntityGraphNode): string | undefined {
  return stringValue(node.properties.Id) ?? node.id;
}

function buildGraphLookup(graphNodes: GraphNode[], graphLinks: EntityGraphEdge[]): GraphLookup {
  const nodesByKey = new Map<string, GraphNode>();
  graphNodes.forEach((node) => {
    getNodeKeys(node).forEach((key) => nodesByKey.set(key, node));
  });

  const ownerByAccountKey = new Map<string, GraphNode>();
  const outboundAccountByTransactionKey = new Map<string, GraphNode>();
  const inboundAccountByTransactionKey = new Map<string, GraphNode>();

  graphLinks.forEach((link) => {
    const sourceNode = nodesByKey.get(link.sourceId);
    const targetNode = nodesByKey.get(link.targetId);

    if (link.type === 'OWNS' && sourceNode && targetNode) {
      getNodeKeys(targetNode).forEach((key) => ownerByAccountKey.set(key, sourceNode));
    }

    if (link.type === 'SENT' && sourceNode && targetNode) {
      getNodeKeys(targetNode).forEach((key) => outboundAccountByTransactionKey.set(key, sourceNode));
    }

    if (link.type === 'RECEIVED_BY' && sourceNode && targetNode) {
      getNodeKeys(sourceNode).forEach((key) => inboundAccountByTransactionKey.set(key, targetNode));
    }
  });

  return {
    nodesByKey,
    ownerByAccountKey,
    outboundAccountByTransactionKey,
    inboundAccountByTransactionKey,
  };
}

function getAccountOwnerName(accountNode: GraphNode, graphLookup: GraphLookup): string | undefined {
  const owner = getNodeKeys(accountNode)
    .map((key) => graphLookup.ownerByAccountKey.get(key))
    .find((candidate): candidate is GraphNode => Boolean(candidate));

  return owner ? stringValue(owner.properties.Name) ?? owner.name : undefined;
}

function getRelatedAccountName(transactionNode: GraphNode, accountByTransactionKey: Map<string, GraphNode>): string | undefined {
  const account = getNodeKeys(transactionNode)
    .map((key) => accountByTransactionKey.get(key))
    .find((candidate): candidate is GraphNode => Boolean(candidate));

  return account ? stringValue(account.properties.IBAN) ?? account.name : undefined;
}

function getPrimaryLabel(node: EntityGraphNode): KnownNodeLabel | undefined {
  const candidates = [node.label, ...node.labels];
  return candidates.find(isKnownNodeLabel);
}

function isKnownNodeLabel(value: string): value is KnownNodeLabel {
  return value === 'Client' ||
    value === 'Account' ||
    value === 'Transaction' ||
    value === 'IpAddress' ||
    value === 'Device';
}

function buildGenericTooltip(node: GraphNode): string {
  const entries = Object.entries(node.properties)
    .filter(([, value]) => value !== null && value !== undefined && value !== '')
    .slice(0, 4)
    .map(([key, value]) => `${key}: ${formatPropertyValue(value)}`);

  return entries.length > 0 ? entries.join(' | ') : `ID ${node.id}`;
}

function formatPropertyValue(value: unknown): string {
  if (value instanceof Date) {
    return value.toISOString();
  }

  if (typeof value === 'string' || typeof value === 'number' || typeof value === 'boolean') {
    return String(value);
  }

  return JSON.stringify(value);
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
