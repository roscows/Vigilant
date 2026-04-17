import { FormEvent, useEffect, useState } from 'react';
import { amlApi } from '../api/amlApi';
import type { AlertRecord, AlertStatus } from '../api/types';
import { useAlertsStore } from '../store/alertsStore';

const statusOptions: AlertStatus[] = ['New', 'UnderReview', 'Resolved', 'FalsePositive'];

interface AlertDetailPanelProps {
  alertId: string | null;
  onClose: () => void;
}

export function AlertDetailPanel({ alertId, onClose }: AlertDetailPanelProps) {
  const upsertAlert = useAlertsStore((state) => state.upsertAlert);
  const [alert, setAlert] = useState<AlertRecord | null>(null);
  const [newStatus, setNewStatus] = useState<AlertStatus>('UnderReview');
  const [analystName, setAnalystName] = useState('');
  const [comment, setComment] = useState('');
  const [isSaving, setIsSaving] = useState(false);

  useEffect(() => {
    if (!alertId) {
      setAlert(null);
      return;
    }

    const abortController = new AbortController();
    amlApi.getAlertById(alertId, abortController.signal)
      .then((data) => {
        setAlert(data);
        setNewStatus(data.status);
      })
      .catch(() => setAlert(null));

    return () => abortController.abort();
  }, [alertId]);

  const submitStatus = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    if (!alert) {
      return;
    }

    setIsSaving(true);
    try {
      const updatedAlert = await amlApi.updateAlertStatus(alert.id, {
        newStatus,
        analystName,
        comment,
      });
      setAlert(updatedAlert);
      upsertAlert(updatedAlert);
      setComment('');
    } finally {
      setIsSaving(false);
    }
  };

  return (
    <aside className={alertId ? 'alert-detail-panel alert-detail-panel--open' : 'alert-detail-panel'} aria-hidden={!alertId}>
      <div className="alert-detail-panel__chrome">
        <button type="button" className="panel-close-button" onClick={onClose}>Close</button>
        {!alert ? (
          <p className="panel-muted">Select an alert to inspect its lifecycle.</p>
        ) : (
          <>
            <header className="alert-detail-header">
              <span className={`severity-badge severity-badge--${alert.severity.toLowerCase()}`}>{alert.severity}</span>
              <h2>{alert.ruleType}</h2>
              <p>{alert.message || 'Persisted AML alert.'}</p>
            </header>

            <dl className="alert-detail-grid">
              <div>
                <dt>Alert ID</dt>
                <dd>{alert.id}</dd>
              </div>
              <div>
                <dt>Status</dt>
                <dd>{alert.status}</dd>
              </div>
              <div>
                <dt>Detected At</dt>
                <dd>{formatDateTime(alert.detectedAt)}</dd>
              </div>
              <div>
                <dt>Involved Account IDs</dt>
                <dd>{alert.involvedAccountIds.length > 0 ? alert.involvedAccountIds.join(', ') : 'No accounts captured'}</dd>
              </div>
            </dl>

            <section className="audit-timeline">
              <h3>Audit Timeline</h3>
              {alert.auditLog.length === 0 ? <p className="panel-muted">No status changes yet.</p> : null}
              {alert.auditLog.map((entry) => (
                <article className="audit-entry" key={`${entry.changedAt}-${entry.toStatus}`}>
                  <strong>{entry.analystName}</strong>
                  <span>{entry.fromStatus} &rarr; {entry.toStatus}</span>
                  <p>{entry.comment || 'No comment provided.'}</p>
                  <time>{formatDateTime(entry.changedAt)}</time>
                </article>
              ))}
            </section>

            <form className="status-update-form" onSubmit={submitStatus}>
              <label>
                Status
                <select value={newStatus} onChange={(event) => setNewStatus(event.target.value as AlertStatus)}>
                  {statusOptions.map((status) => <option key={status} value={status}>{status}</option>)}
                </select>
              </label>
              <label>
                Analyst Name
                <input value={analystName} onChange={(event) => setAnalystName(event.target.value)} placeholder="Analyst name" required />
              </label>
              <label>
                Comment
                <textarea value={comment} onChange={(event) => setComment(event.target.value)} placeholder="Reason for status change" rows={4} />
              </label>
              <button type="submit" disabled={isSaving || analystName.trim().length === 0}>
                {isSaving ? 'Saving...' : 'Update Status'}
              </button>
            </form>
          </>
        )}
      </div>
    </aside>
  );
}

function formatDateTime(value: string): string {
  return new Intl.DateTimeFormat(undefined, {
    dateStyle: 'medium',
    timeStyle: 'short',
  }).format(new Date(value));
}
