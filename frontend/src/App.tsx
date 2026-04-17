import { useCallback, useEffect, useMemo, useState } from 'react';
import { amlApi } from './api/amlApi';
import { AlertDetailPanel } from './components/AlertDetailPanel';
import { AlertsTable } from './components/AlertsTable';
import { AmlGraphViewer } from './components/AmlGraphViewer';
import { startAlertsHub, stopAlertsHub } from './realtime/alertsHub';
import { useAlertsStore } from './store/alertsStore';
import { useGraphStore } from './store/graphStore';

export default function App() {
  const [openAlertId, setOpenAlertId] = useState<string | null>(null);
  const alerts = useAlertsStore((state) => state.alerts);
  const selectedAlertId = useAlertsStore((state) => state.selectedAlertId);
  const addDetectedAlert = useAlertsStore((state) => state.addDetectedAlert);
  const upsertAlert = useAlertsStore((state) => state.upsertAlert);
  const setRealtimeConnected = useAlertsStore((state) => state.setRealtimeConnected);
  const selectAlert = useAlertsStore((state) => state.selectAlert);
  const setGraph = useGraphStore((state) => state.setGraph);
  const setGraphLoading = useGraphStore((state) => state.setLoading);
  const highlightAlert = useGraphStore((state) => state.highlightAlert);

  const selectedAlert = useMemo(
    () => alerts.find((alert) => alert.id === selectedAlertId) ?? null,
    [alerts, selectedAlertId],
  );

  const loadGraphOverview = useCallback(async (signal?: AbortSignal) => {
    setGraphLoading(true);
    selectAlert(null);
    highlightAlert(null);

    try {
      const graph = await amlApi.getGraph({ limit: 500 }, signal);
      setGraph(graph);
    } catch {
      setGraph({ nodes: [], edges: [] });
    }
  }, [highlightAlert, selectAlert, setGraph, setGraphLoading]);

  const searchAccountIban = useCallback(async (iban: string) => {
    setGraphLoading(true);
    selectAlert(null);
    highlightAlert(null);

    try {
      const graph = await amlApi.getGraph({ ibanFocus: iban, depth: 2, limit: 500 });
      setGraph(graph);
    } catch {
      setGraph({ nodes: [], edges: [] });
    }
  }, [highlightAlert, selectAlert, setGraph, setGraphLoading]);

  useEffect(() => {
    const abortController = new AbortController();
    void loadGraphOverview(abortController.signal);

    startAlertsHub({
      onDetectedAlert: addDetectedAlert,
      onUpdatedAlert: (alert) => upsertAlert(alert),
      onConnectionChange: setRealtimeConnected,
    }).catch(() => setRealtimeConnected(false));

    return () => {
      abortController.abort();
      setRealtimeConnected(false);
      void stopAlertsHub();
    };
  }, [addDetectedAlert, loadGraphOverview, setRealtimeConnected, upsertAlert]);

  const openAlert = (alertId: string) => {
    setOpenAlertId(alertId);
  };

  const closeAlert = () => {
    setOpenAlertId(null);
  };

  return (
    <main className="analyst-dashboard analyst-dashboard--table">
      <AlertsTable onOpenAlert={openAlert} />
      <AmlGraphViewer
        onLoadOverview={loadGraphOverview}
        onSearchIban={searchAccountIban}
        highlightedAccountIds={openAlertId && selectedAlert ? selectedAlert.involvedAccountIds : []}
      />
      <AlertDetailPanel alertId={openAlertId} onClose={closeAlert} />
    </main>
  );
}