import type { AlertRecord, AlertSeverity, AlertSeverityFilter } from '../api/types';
import { useAlertsStore } from '../store/alertsStore';
import { useGraphStore } from '../store/graphStore';

const filters: AlertSeverityFilter[] = ['All', 'Critical', 'High', 'Medium'];

const alertTypeLabels: Record<string, string> = {
  CircularFlow: 'Circular Flow',
  Smurfing: 'Smurfing / Structuring',
  RapidFanOut: 'Rapid Fan-Out',
  SharedDeviceOrIp: 'Shared Device or IP',
  RoundTrip: 'Boomerang / Round-trip',
  PepOffshore: 'PEP + Offshore',
};

export function AlertsPanel() {
  const alerts = useAlertsStore((state) => state.alerts);
  const unreadCount = useAlertsStore((state) => state.unreadCount);
  const selectedAlertId = useAlertsStore((state) => state.selectedAlertId);
  const severityFilter = useAlertsStore((state) => state.severityFilter);
  const isLoading = useAlertsStore((state) => state.isLoading);
  const setFilter = useAlertsStore((state) => state.setFilter);
  const selectAlert = useAlertsStore((state) => state.selectAlert);
  const markRead = useAlertsStore((state) => state.markRead);
  const highlightAlert = useGraphStore((state) => state.highlightAlert);

  const filteredAlerts = alerts.filter((alert) => severityFilter === 'All' || alert.severity === severityFilter);

  const investigate = (alert: AlertRecord) => {
    selectAlert(alert.id);
    highlightAlert(alert.id);
    markRead();
  };

  return (
    <aside className="alerts-rail">
      <header className="alerts-brand">
        <div className="brand-lockup">
          <img className="brand-icon" src="/vigilant-icon.svg" alt="" aria-hidden="true" />
          <div>
            <strong>Vigilant</strong>
          </div>
        </div>
      </header>

      <nav className="severity-tabs" aria-label="Alert severity filter">
        {filters.map((filter) => (
          <button
            className={severityFilter === filter ? 'severity-tab severity-tab--active' : 'severity-tab'}
            key={filter}
            onClick={() => setFilter(filter)}
            type="button"
          >
            <span>{filter}</span>
            {filter === 'All' && unreadCount > 0 ? <b>{unreadCount}</b> : null}
          </button>
        ))}
      </nav>

      <section className="alert-feed" aria-label="AML alert feed">
        {isLoading ? <AlertSkeletons /> : null}
        {!isLoading && filteredAlerts.length === 0 ? (
          <p className="empty-alert-feed">No alerts for this severity. Seed demo data or wait for live detections.</p>
        ) : null}
        {!isLoading && filteredAlerts.map((alert) => (
          <article
            className={selectedAlertId === alert.id ? 'alert-card alert-card--selected' : 'alert-card'}
            key={alert.id}
          >
            <div className="alert-card__topline">
              <SeverityBadge severity={alert.severity} />
              <span>{formatRelativeTime(alert.detectedAt)}</span>
            </div>
            <h3>{alertTypeLabels[alert.ruleType] ?? alert.ruleType}</h3>
            <p>{alert.message}</p>
            <button className="investigate-button" type="button" onClick={() => investigate(alert)}>
              Investigate
            </button>
          </article>
        ))}
      </section>
    </aside>
  );
}

function SeverityBadge({ severity }: { severity: AlertSeverity }) {
  return <strong className={`severity-badge severity-badge--${severity.toLowerCase()}`}>{severity}</strong>;
}

function AlertSkeletons() {
  return (
    <>
      {Array.from({ length: 5 }, (_, index) => (
        <article className="alert-card alert-card--skeleton" key={index}>
          <span />
          <strong />
          <p />
          <i />
        </article>
      ))}
    </>
  );
}

function formatRelativeTime(value: string): string {
  const timestamp = new Date(value).getTime();
  const deltaSeconds = Math.max(0, Math.floor((Date.now() - timestamp) / 1000));

  if (deltaSeconds < 60) {
    return `${deltaSeconds || 1} sec ago`;
  }

  const deltaMinutes = Math.floor(deltaSeconds / 60);
  if (deltaMinutes < 60) {
    return `${deltaMinutes} min ago`;
  }

  const deltaHours = Math.floor(deltaMinutes / 60);
  if (deltaHours < 24) {
    return `${deltaHours} h ago`;
  }

  return `${Math.floor(deltaHours / 24)} d ago`;
}
