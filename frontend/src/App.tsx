import { useEffect, useState } from 'react';
import { amlApi } from './api/amlApi';
import { AlertsPanel } from './components/AlertsPanel';
import { AmlGraphViewer } from './components/AmlGraphViewer';
import { MetricCard } from './components/MetricCard';
import { useGraphData } from './hooks/useGraphData';
import { startAlertsHub, stopAlertsHub } from './realtime/alertsHub';
import { useAlertsStore } from './store/alertsStore';

const defaultIban = 'RS35105008123123123173';

export default function App() {
  const [accountIban, setAccountIban] = useState(defaultIban);
  const [isSeeding, setIsSeeding] = useState(false);
  const [seedMessage, setSeedMessage] = useState<string | null>(null);
  const { forceGraphData, isLoading, error, refresh } = useGraphData(accountIban, 5);
  const { alerts, isRealtimeConnected, prependAlerts, setAlerts, setRealtimeConnected } = useAlertsStore();

  useEffect(() => {
    let isMounted = true;

    amlApi.getAlerts({ maxTransfers: 4, lookbackHours: 24, limit: 10 })
      .then((data) => {
        if (isMounted) {
          setAlerts(data);
        }
      })
      .catch(() => undefined);

    startAlertsHub((incomingAlerts) => prependAlerts(incomingAlerts))
      .then(() => setRealtimeConnected(true))
      .catch(() => setRealtimeConnected(false));

    return () => {
      isMounted = false;
      setRealtimeConnected(false);
      void stopAlertsHub();
    };
  }, [prependAlerts, setAlerts, setRealtimeConnected]);

  const seedDemoGraph = async () => {
    setIsSeeding(true);
    setSeedMessage(null);

    try {
      const result = await amlApi.seedTransactions({
        clientCount: 20,
        accountCount: 34,
        randomTransactionCount: 72,
        circularFlowCount: 3,
      });

      setAccountIban(result.focusAccountIban);
      setAlerts(result.triggeredAlerts);
      setSeedMessage(
        `Seeded ${result.transactionsCreated} transactions and ${result.circularFlowsCreated} circular flows. Focus moved to ${result.focusAccountIban}.`,
      );
    } catch (error) {
      setSeedMessage(error instanceof Error ? error.message : 'Unable to seed demo graph.');
    } finally {
      setIsSeeding(false);
    }
  };

  return (
    <main className="dashboard-shell">
      <header className="app-topbar glass-panel">
        <div className="brand-lockup" aria-label="Vigilant application brand">
          <span className="brand-mark">V</span>
          <div>
            <strong>Vigilant</strong>
            <small>AML Graph Intelligence</small>
          </div>
        </div>
        <span className="brand-tagline">Graph-powered financial crime detection</span>
      </header>

      <section className="hero-panel glass-panel">
        <div>
          <p className="eyebrow">Vigilant AML Platform</p>
          <h1>Vigilant follows suspicious money until the pattern gives itself away.</h1>
          <p className="hero-copy">
            Vigilant generates realistic Neo4j AML graphs, visualizes entity relationships, and streams circular-flow alerts in real time.
          </p>
          <div className="hero-actions">
            <button onClick={() => void seedDemoGraph()} disabled={isSeeding}>
              {isSeeding ? 'Seeding graph...' : 'Seed Demo Graph'}
            </button>
            <button className="ghost-button" onClick={() => void refresh()}>
              Refresh Current Graph
            </button>
          </div>
          {seedMessage ? <p className="seed-message">{seedMessage}</p> : null}
        </div>
        <div className="status-card">
          <span className={isRealtimeConnected ? 'pulse online' : 'pulse'} />
          <span>{isRealtimeConnected ? 'Realtime alerts online' : 'Realtime alerts connecting'}</span>
        </div>
      </section>

      <section className="control-grid">
        <article className="glass-panel search-card">
          <label htmlFor="iban">Account IBAN</label>
          <div className="search-row">
            <input
              id="iban"
              value={accountIban}
              onChange={(event) => setAccountIban(event.target.value)}
              placeholder="Enter account IBAN"
            />
            <button onClick={() => void refresh()}>Load Graph</button>
          </div>
        </article>

        <MetricCard label="Nodes" value={forceGraphData.nodes.length} tone="blue" />
        <MetricCard label="Edges" value={forceGraphData.links.length} tone="mint" />
        <MetricCard label="Alerts" value={alerts.length} tone="coral" />
      </section>

      <section className="content-grid">
        <AmlGraphViewer data={forceGraphData} isLoading={isLoading || isSeeding} error={error} />
        <AlertsPanel alerts={alerts} />
      </section>
    </main>
  );
}



