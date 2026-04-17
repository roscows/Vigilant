import { create } from 'zustand';
import type { EntityGraph, EntityGraphEdge, EntityGraphNode } from '../api/types';
import { useAlertsStore } from './alertsStore';

interface GraphState {
  nodes: EntityGraphNode[];
  links: EntityGraphEdge[];
  loading: boolean;
  highlightedAlertId: string | null;
  highlightedNodeKeys: string[];
  setGraph: (graph: EntityGraph) => void;
  setLoading: (loading: boolean) => void;
  highlightAlert: (alertId: string | null) => void;
}

export const useGraphStore = create<GraphState>((set) => ({
  nodes: [],
  links: [],
  loading: false,
  highlightedAlertId: null,
  highlightedNodeKeys: [],
  setGraph: (graph) => set({ nodes: graph.nodes, links: graph.edges, loading: false }),
  setLoading: (loading) => set({ loading }),
  highlightAlert: (alertId) => {
    const alert = alertId
      ? useAlertsStore.getState().alerts.find((candidate) => candidate.id === alertId)
      : undefined;

    set({
      highlightedAlertId: alertId,
      highlightedNodeKeys: alert?.involvedAccountIds ?? [],
    });
  },
}));