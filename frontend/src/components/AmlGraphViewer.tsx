import { useEffect, useMemo, useRef, useState } from 'react';
import ForceGraph2D, { type ForceGraphMethods, type LinkObject, type NodeObject } from 'react-force-graph-2d';
import type { ForceGraphData, ForceGraphLink, ForceGraphNode } from '../hooks/useGraphData';

interface AmlGraphViewerProps {
  data: ForceGraphData;
  isLoading: boolean;
  error: string | null;
}

const nodePalette: Record<string, string> = {
  Client: '#b86cff',
  Account: '#4ea3ff',
  Transaction: '#35e07f',
  IpAddress: '#8b99a8',
  Device: '#66717f',
};

const linkPalette: Record<string, string> = {
  OWNS: 'rgba(184, 108, 255, 0.55)',
  SENT: 'rgba(78, 163, 255, 0.6)',
  RECEIVED_BY: 'rgba(53, 224, 127, 0.55)',
  EXECUTED_FROM_IP: 'rgba(139, 153, 168, 0.38)',
  EXECUTED_ON_DEVICE: 'rgba(102, 113, 127, 0.38)',
};

export function AmlGraphViewer({ data, isLoading, error }: AmlGraphViewerProps) {
  const graphRef = useRef<ForceGraphMethods<ForceGraphNode, ForceGraphLink> | undefined>(undefined);
  const containerRef = useRef<HTMLDivElement | null>(null);
  const [hoveredNode, setHoveredNode] = useState<ForceGraphNode | null>(null);
  const [size, setSize] = useState({ width: 900, height: 520 });

  useEffect(() => {
    if (!containerRef.current) {
      return;
    }

    const observer = new ResizeObserver(([entry]) => {
      const width = Math.max(320, Math.floor(entry.contentRect.width));
      const height = Math.max(420, Math.floor(entry.contentRect.height));
      setSize({ width, height });
    });

    observer.observe(containerRef.current);
    return () => observer.disconnect();
  }, []);

  useEffect(() => {
    if (data.nodes.length === 0) {
      return;
    }

    const timeoutId = window.setTimeout(() => {
      graphRef.current?.zoomToFit(650, 70);
    }, 300);

    return () => window.clearTimeout(timeoutId);
  }, [data.nodes.length]);

  const legend = useMemo(() => ['Client', 'Account', 'Transaction', 'IpAddress', 'Device'], []);

  return (
    <article className="glass-panel graph-preview">
      <div className="panel-heading graph-heading">
        <div>
          <p className="eyebrow">Entity Network</p>
          <h2>Interactive AML graph</h2>
        </div>
        <div className="graph-actions">
          {isLoading ? <span className="loading-chip">Loading</span> : null}
          <button className="ghost-button" onClick={() => graphRef.current?.zoomToFit(500, 80)}>
            Fit view
          </button>
        </div>
      </div>

      <div className="graph-legend" aria-label="Graph entity legend">
        {legend.map((label) => (
          <span key={label}>
            <i style={{ background: nodePalette[label] }} />
            {label}
          </span>
        ))}
      </div>

      <div className="force-graph-frame" ref={containerRef}>
        {data.nodes.length === 0 ? (
          <div className="graph-empty-state">
            <strong>No graph loaded</strong>
            <span>Seed demo data or search an account IBAN to load the network.</span>
          </div>
        ) : (
          <ForceGraph2D<ForceGraphNode, ForceGraphLink>
            ref={graphRef}
            graphData={data}
            width={size.width}
            height={size.height}
            backgroundColor="rgba(3, 8, 10, 0)"
            nodeId="id"
            nodeVal="val"
            nodeLabel={(node) => buildNodeTooltip(node as ForceGraphNode)}
            nodeCanvasObject={paintNode}
            nodePointerAreaPaint={paintNodePointerArea}
            onNodeHover={(node) => setHoveredNode((node as ForceGraphNode | null) ?? null)}
            linkSource="source"
            linkTarget="target"
            linkLabel={(link) => (link as ForceGraphLink).label}
            linkColor={(link) => linkPalette[(link as ForceGraphLink).type] ?? 'rgba(220, 247, 242, 0.26)'}
            linkWidth={(link) => ((link as ForceGraphLink).type === 'SENT' ? 1.8 : 1.1)}
            linkCurvature={0.08}
            linkDirectionalArrowLength={4}
            linkDirectionalArrowRelPos={1}
            linkDirectionalParticles={(link) => ((link as ForceGraphLink).type === 'SENT' ? 2 : 0)}
            linkDirectionalParticleSpeed={0.006}
            linkDirectionalParticleWidth={1.9}
            minZoom={0.35}
            maxZoom={5}
            cooldownTicks={90}
            d3VelocityDecay={0.28}
            enableNodeDrag
            enablePanInteraction
            enableZoomInteraction
          />
        )}

        {hoveredNode ? (
          <div className="node-hover-card">
            <span>{hoveredNode.label}</span>
            <strong>{hoveredNode.name}</strong>
            <p>{buildNodeSummary(hoveredNode)}</p>
          </div>
        ) : null}
      </div>

      {error ? <p className="error-copy">{error}</p> : null}
    </article>
  );
}

function paintNode(node: NodeObject<ForceGraphNode>, ctx: CanvasRenderingContext2D, globalScale: number) {
  const amlNode = node as ForceGraphNode;
  const x = node.x ?? 0;
  const y = node.y ?? 0;
  const radius = getNodeRadius(amlNode);
  const color = getNodeColor(amlNode);
  const label = amlNode.label === 'Transaction' ? '' : amlNode.name;
  const fontSize = Math.max(3.5, 11 / globalScale);

  ctx.save();
  ctx.beginPath();
  ctx.arc(x, y, radius, 0, 2 * Math.PI, false);
  ctx.fillStyle = color;
  ctx.shadowBlur = amlNode.label === 'Transaction' ? 16 : 22;
  ctx.shadowColor = color;
  ctx.fill();

  ctx.lineWidth = 1.5 / globalScale;
  ctx.strokeStyle = 'rgba(255, 255, 255, 0.78)';
  ctx.stroke();

  if (label && globalScale > 0.65) {
    ctx.font = `600 ${fontSize}px Aptos, Segoe UI, sans-serif`;
    ctx.textAlign = 'center';
    ctx.textBaseline = 'top';
    ctx.fillStyle = 'rgba(237, 248, 244, 0.82)';
    ctx.shadowBlur = 0;
    ctx.fillText(truncate(label, 24), x, y + radius + 3 / globalScale);
  }

  ctx.restore();
}

function paintNodePointerArea(node: NodeObject<ForceGraphNode>, color: string, ctx: CanvasRenderingContext2D) {
  const amlNode = node as ForceGraphNode;
  ctx.fillStyle = color;
  ctx.beginPath();
  ctx.arc(node.x ?? 0, node.y ?? 0, getNodeRadius(amlNode) + 5, 0, 2 * Math.PI, false);
  ctx.fill();
}

function getNodeRadius(node: ForceGraphNode): number {
  if (node.label === 'Client') {
    return 8.5;
  }

  if (node.label === 'Account') {
    return 7.5;
  }

  if (node.label === 'Transaction') {
    const amount = Number(node.properties.Amount ?? 0);
    return amount >= 10_000 ? 5.8 : 4.2;
  }

  return 5.4;
}

function getNodeColor(node: ForceGraphNode): string {
  if (node.label === 'Transaction') {
    const amount = Number(node.properties.Amount ?? 0);
    return amount >= 10_000 ? '#ff4d64' : '#35e07f';
  }

  return nodePalette[node.label] ?? '#d7f7ef';
}

function buildNodeTooltip(node: ForceGraphNode): string {
  return `${node.label}: ${node.name}\n${buildNodeSummary(node)}`;
}

function buildNodeSummary(node: ForceGraphNode): string {
  const properties = node.properties;

  if (node.label === 'Account') {
    return `IBAN ${String(properties.IBAN ?? 'unknown')} - Balance ${formatNumber(properties.Balance)}`;
  }

  if (node.label === 'Transaction') {
    return `${String(properties.Currency ?? 'EUR')} ${formatNumber(properties.Amount)} - ${String(properties.Timestamp ?? '')}`;
  }

  if (node.label === 'Client') {
    return `Risk score ${formatNumber(properties.RiskScore)}`;
  }

  if (node.label === 'IpAddress') {
    return `IP ${String(properties.Address ?? node.name)} - ${String(properties.CountryCode ?? 'N/A')}`;
  }

  if (node.label === 'Device') {
    return `Device ${String(properties.DeviceId ?? node.name)}`;
  }

  return node.id;
}

function formatNumber(value: unknown): string {
  const numericValue = Number(value ?? 0);
  return Number.isFinite(numericValue) ? numericValue.toLocaleString('en-US') : '0';
}

function truncate(value: string, maxLength: number): string {
  return value.length > maxLength ? `${value.slice(0, maxLength - 1)}...` : value;
}



