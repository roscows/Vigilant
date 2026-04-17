import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { amlApi } from '../api/amlApi';
import type { EntityGraph, EntityGraphEdge, EntityGraphNode } from '../api/types';

export interface ForceGraphNode extends EntityGraphNode {
  name: string;
  val: number;
  color: string;
}

export interface ForceGraphLink extends EntityGraphEdge {
  source: string;
  target: string;
  label: string;
}

export interface ForceGraphData {
  nodes: ForceGraphNode[];
  links: ForceGraphLink[];
}

interface GraphDataState {
  data: EntityGraph | null;
  isLoading: boolean;
  error: string | null;
}

const nodeColors: Record<string, string> = {
  Client: '#b86cff',
  Account: '#4ea3ff',
  Transaction: '#35e07f',
  IpAddress: '#8b99a8',
  Device: '#66717f',
};

export function useGraphData(accountIban: string | null, depth = 4) {
  const abortRef = useRef<AbortController | null>(null);
  const [state, setState] = useState<GraphDataState>({
    data: null,
    isLoading: false,
    error: null,
  });

  const load = useCallback(async () => {
    if (!accountIban?.trim()) {
      setState({ data: null, isLoading: false, error: null });
      return;
    }

    abortRef.current?.abort();
    const abortController = new AbortController();
    abortRef.current = abortController;

    setState((current) => ({ ...current, isLoading: true, error: null }));

    try {
      const data = await amlApi.getEntityGraph(accountIban.trim(), depth, abortController.signal);
      setState({ data, isLoading: false, error: null });
    } catch (error) {
      if (abortController.signal.aborted) {
        return;
      }

      setState({
        data: null,
        isLoading: false,
        error: error instanceof Error ? error.message : 'Unable to load graph data.',
      });
    }
  }, [accountIban, depth]);

  useEffect(() => {
    void load();

    return () => {
      abortRef.current?.abort();
    };
  }, [load]);

  const forceGraphData = useMemo<ForceGraphData>(() => {
    if (!state.data) {
      return { nodes: [], links: [] };
    }

    return {
      nodes: state.data.nodes.map((node) => ({
        ...node,
        name: getNodeName(node),
        val: getNodeWeight(node),
        color: nodeColors[node.label] ?? '#d7f7ef',
      })),
      links: state.data.edges.map((edge) => ({
        ...edge,
        source: edge.sourceId,
        target: edge.targetId,
        label: edge.type,
      })),
    };
  }, [state.data]);

  return {
    ...state,
    forceGraphData,
    refresh: load,
  };
}

function getNodeName(node: EntityGraphNode): string {
  const properties = node.properties;
  return String(
    properties.Name ??
      properties.IBAN ??
      properties.Id ??
      properties.Address ??
      properties.DeviceId ??
      node.id,
  );
}

function getNodeWeight(node: EntityGraphNode): number {
  if (node.label === 'Transaction') {
    const amount = Number(node.properties.Amount ?? 0);
    return amount >= 10_000 ? 8 : 5;
  }

  if (node.label === 'Account') {
    return 9;
  }

  if (node.label === 'Client') {
    return 10;
  }

  return 6;
}

