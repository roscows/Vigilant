import type { AmlAlert } from '../api/types';

interface AlertsPanelProps {
  alerts: AmlAlert[];
}

export function AlertsPanel({ alerts }: AlertsPanelProps) {
  return (
    <aside className="glass-panel alerts-panel">
      <div className="panel-heading">
        <div>
          <p className="eyebrow">Rule Engine</p>
          <h2>Circular-flow alerts</h2>
        </div>
        <span className="alert-count">{alerts.length}</span>
      </div>

      <div className="alert-list">
        {alerts.length === 0 ? (
          <p className="empty-state">No circular-flow alerts yet. Seed demo data to trigger the laundering ring.</p>
        ) : (
          alerts.map((alert) => (
            <article className={`alert-item alert-item--${alert.severity.toLowerCase()}`} key={alert.id}>
              <div className="alert-item__topline">
                <strong>{alert.severity}</strong>
                <span>{formatMoney(alert.totalAmount)}</span>
              </div>
              <p>{alert.message}</p>
              <small>{alert.accountIban}</small>
            </article>
          ))
        )}
      </div>
    </aside>
  );
}

function formatMoney(value: number): string {
  return new Intl.NumberFormat('en-US', {
    style: 'currency',
    currency: 'EUR',
    maximumFractionDigits: 0,
  }).format(value);
}

