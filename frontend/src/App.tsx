import { useCallback, useEffect, useMemo } from 'react';
import { amlApi } from './api/amlApi';
import { AlertsPanel } from './components/AlertsPanel';
import { AmlGraphViewer } from './components/AmlGraphViewer';
import { startAlertsHub, stopAlertsHub } from './realtime/alertsHub';
import { useAlertsStore } from './store/alertsStore';
import { useGraphStore } from './store/graphStore';

export default function App() {
  const alerts = useAlertsStore((state) => state.alerts);
  const selectedAlertId = useAlertsStore((state) => state.selectedAlertId);
  const setAlerts = useAlertsStore((state) => state.setAlerts);
  const addAlert = useAlertsStore((state) => state.addAlert);
  const setAlertsLoading = useAlertsStore((state) => state.setLoading);
  const setRealtimeConnected = useAlertsStore((state) => state.setRealtimeConnected);
  const setGraph = useGraphStore((state) => state.setGraph);
  const setGraphLoading = useGraphStore((state) => state.setLoading);
  const highlightAlert = useGraphStore((state) => state.highlightAlert);

  const selectedAlert = useMemo(
    () => alerts.find((alert) => alert.id === selectedAlertId),
    [alerts, selectedAlertId],
  );

  const loadGraphOverview = useCallback(async (signal?: AbortSignal) => {
    setGraphLoading(true);
    useAlertsStore.getState().selectAlert(null);
    highlightAlert(null);

    try {
      const graph = await amlApi.getGraphOverview(250, signal);
      setGraph(graph);
    } catch {
      setGraph({ nodes: [], edges: [] });
    }
  }, [highlightAlert, setGraph, setGraphLoading]);

  const searchAccountIban = useCallback(async (iban: string) => {
    setGraphLoading(true);
    useAlertsStore.getState().selectAlert(null);
    highlightAlert(null);

    try {
      const graph = await amlApi.getEntityGraph(iban, 6);
      setGraph(graph);
    } catch {
      setGraph({ nodes: [], edges: [] });
    }
  }, [highlightAlert, setGraph, setGraphLoading]);

  useEffect(() => {
    const abortController = new AbortController();
    setAlertsLoading(true);

    amlApi.getAlerts({ maxTransfers: 8, lookbackHours: 24 * 7, limit: 250 }, abortController.signal)
      .then((data) => setAlerts(data))
      .catch(() => setAlerts([]));

    void loadGraphOverview(abortController.signal);

    startAlertsHub({
      onAlert: addAlert,
      onConnectionChange: setRealtimeConnected,
    }).catch(() => setRealtimeConnected(false));

    return () => {
      abortController.abort();
      setRealtimeConnected(false);
      void stopAlertsHub();
    };
  }, [addAlert, loadGraphOverview, setAlerts, setAlertsLoading, setRealtimeConnected]);

  useEffect(() => {
    if (!selectedAlert?.accountIban) {
      return;
    }

    const abortController = new AbortController();
    setGraphLoading(true);

    amlApi.getEntityGraph(selectedAlert.accountIban, 6, abortController.signal)
      .then((graph) => {
        setGraph(graph);
        highlightAlert(selectedAlert.id);
      })
      .catch(() => setGraph({ nodes: [], edges: [] }));

    return () => abortController.abort();
  }, [highlightAlert, selectedAlert, setGraph, setGraphLoading]);

  return (
    <main className="analyst-dashboard">
      <AlertsPanel />
      <AmlGraphViewer onLoadOverview={loadGraphOverview} onSearchIban={searchAccountIban} />
    </main>
  );
}