import { useEffect, useMemo, useState } from 'react';
import { amlApi } from '../api/amlApi';
import type { AlertRecord, AlertSeverity, AlertStatus, AlertStatusFilter } from '../api/types';
import { useAlertsStore } from '../store/alertsStore';
import { useGraphStore } from '../store/graphStore';

const statusOptions: AlertStatusFilter[] = ['All', 'New', 'UnderReview', 'Resolved', 'FalsePositive'];

interface AlertsTableProps {
  onOpenAlert: (alertId: string) => void;
}

export function AlertsTable({ onOpenAlert }: AlertsTableProps) {
  const alerts = useAlertsStore((state) => state.alerts);
  const isLoading = useAlertsStore((state) => state.isLoading);
  const statusFilter = useAlertsStore((state) => state.statusFilter);
  const recentAlertIds = useAlertsStore((state) => state.recentAlertIds);
  const setAlerts = useAlertsStore((state) => state.setAlerts);
  const setStatusFilter = useAlertsStore((state) => state.setStatusFilter);
  const setLoading = useAlertsStore((state) => state.setLoading);
  const selectAlert = useAlertsStore((state) => state.selectAlert);
  const highlightAlert = useGraphStore((state) => state.highlightAlert);
  const [from, setFrom] = useState('');
  const [to, setTo] = useState('');

  useEffect(() => {
    const abortController = new AbortController();
    setLoading(true);

    amlApi.getAlerts({
      status: statusFilter === 'All' ? undefined : statusFilter,
      from: toIsoDate(from, false),
      to: toIsoDate(to, true),
    }, abortController.signal)
      .then((data) => setAlerts(data))
      .catch(() => setAlerts([]));

    return () => abortController.abort();
  }, [from, setAlerts, setLoading, statusFilter, to]);

  const sortedAlerts = useMemo(
    () => [...alerts].sort((left, right) => new Date(right.detectedAt).getTime() - new Date(left.detectedAt).getTime()),
    [alerts],
  );

  const openAlert = (alert: AlertRecord) => {
    selectAlert(alert.id);
    highlightAlert(alert.id);
    onOpenAlert(alert.id);
  };

  return (
    <aside className="alerts-table-panel glass-panel">
      <div className="alerts-table-panel__header">
        <div className="brand-lockup">
          <img className="brand-icon" src="/vigilant-icon.svg" alt="" aria-hidden="true" />
          <div>
            <strong>Vigilant</strong>
          </div>
        </div>
      </div>

      <div className="alert-filters">
        <label>
          Status
          <select value={statusFilter} onChange={(event) => setStatusFilter(event.target.value as AlertStatusFilter)}>
            {statusOptions.map((option) => <option key={option} value={option}>{option}</option>)}
          </select>
        </label>
        <label>
          From
          <input type="date" value={from} onChange={(event) => setFrom(event.target.value)} />
        </label>
        <label>
          To
          <input type="date" value={to} onChange={(event) => setTo(event.target.value)} />
        </label>
      </div>

      <div className="alerts-table-wrap">
        <table className="alerts-table">
          <thead>
            <tr>
              <th>ID</th>
              <th>Rule Type</th>
              <th>Severity</th>
              <th>Status</th>
              <th>Detected At</th>
              <th>Actions</th>
            </tr>
          </thead>
          <tbody>
            {isLoading ? <SkeletonRows /> : null}
            {!isLoading && sortedAlerts.length === 0 ? (
              <tr>
                <td colSpan={6} className="alerts-table__empty">No persisted alerts match the current filters.</td>
              </tr>
            ) : null}
            {!isLoading && sortedAlerts.map((alert) => (
              <tr className={recentAlertIds.includes(alert.id) ? 'alert-row alert-row--fresh' : 'alert-row'} key={alert.id}>
                <td>{truncate(alert.id, 8)}</td>
                <td>{formatRuleType(alert.ruleType)}</td>
                <td><SeverityBadge severity={alert.severity} /></td>
                <td><StatusPill status={alert.status} /></td>
                <td>{formatDateTime(alert.detectedAt)}</td>
                <td>
                  <button type="button" className="table-action-button" onClick={() => openAlert(alert)}>
                    Open
                  </button>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </aside>
  );
}

function SkeletonRows() {
  return (
    <>
      {Array.from({ length: 5 }, (_, index) => (
        <tr className="alert-row alert-row--skeleton" key={index}>
          <td colSpan={6}><span /></td>
        </tr>
      ))}
    </>
  );
}

function SeverityBadge({ severity }: { severity: AlertSeverity }) {
  return <strong className={`severity-badge severity-badge--${severity.toLowerCase()}`}>{severity}</strong>;
}

function StatusPill({ status }: { status: AlertStatus }) {
  return <span className={`status-pill status-pill--${status.toLowerCase()}`}>{status}</span>;
}

function toIsoDate(value: string, endOfDay: boolean): string | undefined {
  if (!value) {
    return undefined;
  }

  return `${value}T${endOfDay ? '23:59:59' : '00:00:00'}.000Z`;
}

function formatRuleType(value: string): string {
  return value.replace(/([a-z])([A-Z])/g, '$1 $2');
}

function formatDateTime(value: string): string {
  return new Intl.DateTimeFormat(undefined, {
    dateStyle: 'medium',
    timeStyle: 'short',
  }).format(new Date(value));
}

function truncate(value: string, length: number): string {
  return value.length > length ? `${value.slice(0, length)}...` : value;
}
