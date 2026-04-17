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
  const accountFocus = useAlertsStore((state) => state.accountFocus);
  const recentAlertIds = useAlertsStore((state) => state.recentAlertIds);
  const setAlerts = useAlertsStore((state) => state.setAlerts);
  const setStatusFilter = useAlertsStore((state) => state.setStatusFilter);
  const setAccountFocus = useAlertsStore((state) => state.setAccountFocus);
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
    () => {
      const focusedAccountIds = new Set(accountFocus?.accountIds.map((accountId) => accountId.toLowerCase()) ?? []);
      const filteredAlerts = focusedAccountIds.size === 0
        ? alerts
        : alerts.filter((alert) => alert.involvedAccountIds.some((accountId) => focusedAccountIds.has(accountId.toLowerCase())));

      return [...filteredAlerts].sort((left, right) => new Date(right.detectedAt).getTime() - new Date(left.detectedAt).getTime());
    },
    [accountFocus, alerts],
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

      <div className="account-alert-focus">
        <label htmlFor="account-alert-focus">Graph Focus</label>
        <div>
          <input
            id="account-alert-focus"
            readOnly
            value={accountFocus?.ibanQuery ?? ''}
            placeholder="Click an account or client node"
          />
          <button type="button" onClick={() => setAccountFocus(null)} disabled={!accountFocus}>
            Clear
          </button>
        </div>
        {accountFocus ? (
          <span>
            Showing suspicious alerts for {accountFocus.sourceLabel} across {accountFocus.accountIds.length} account{accountFocus.accountIds.length === 1 ? '' : 's'}.
          </span>
        ) : null}
      </div>

      <div className="alerts-table-shell">
        <div className="alerts-table-header" role="row">
          <span role="columnheader">ID</span>
          <span role="columnheader">Rule Type</span>
          <span role="columnheader">Severity</span>
          <span role="columnheader">Status</span>
          <span role="columnheader">Detected At</span>
        </div>

        <div className="alerts-table-wrap">
          <table className="alerts-table" aria-label="Persisted AML alerts">
          <tbody>
            {isLoading ? <SkeletonRows /> : null}
            {!isLoading && sortedAlerts.length === 0 ? (
              <tr>
                <td colSpan={5} className="alerts-table__empty">No persisted alerts match the current filters.</td>
              </tr>
            ) : null}
            {!isLoading && sortedAlerts.map((alert) => (
              <tr
                className={recentAlertIds.includes(alert.id) ? 'alert-row alert-row--fresh' : 'alert-row'}
                key={alert.id}
                onClick={() => openAlert(alert)}
                onKeyDown={(event) => {
                  if (event.key === 'Enter' || event.key === ' ') {
                    event.preventDefault();
                    openAlert(alert);
                  }
                }}
                role="button"
                tabIndex={0}
                title="Open alert details"
              >
                <td>{truncate(alert.id, 8)}</td>
                <td>{formatRuleType(alert.ruleType)}</td>
                <td><SeverityBadge severity={alert.severity} /></td>
                <td><StatusPill status={alert.status} /></td>
                <td>{formatDateTime(alert.detectedAt)}</td>
              </tr>
            ))}
          </tbody>
          </table>
        </div>
      </div>
    </aside>
  );
}

function SkeletonRows() {
  return (
    <>
      {Array.from({ length: 5 }, (_, index) => (
        <tr className="alert-row alert-row--skeleton" key={index}>
          <td colSpan={5}><span /></td>
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
