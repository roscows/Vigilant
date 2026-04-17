import { FormEvent, useEffect, useMemo, useState } from 'react';
import { amlApi } from '../api/amlApi';
import type { AlertRecord, AlertStatus, EntityGraph, EntityGraphEdge, EntityGraphNode } from '../api/types';
import { useAlertsStore } from '../store/alertsStore';
import { useGraphStore } from '../store/graphStore';

const statusOptions: AlertStatus[] = ['New', 'UnderReview', 'Resolved', 'FalsePositive'];

interface InvolvedCustomerAccount {
  accountId: string;
  iban: string;
  clientName: string;
  riskScore: number;
}

interface AlertDetailPanelProps {
  alertId: string | null;
  onClose: () => void;
}

export function AlertDetailPanel({ alertId, onClose }: AlertDetailPanelProps) {
  const upsertAlert = useAlertsStore((state) => state.upsertAlert);
  const graphNodes = useGraphStore((state) => state.nodes);
  const graphLinks = useGraphStore((state) => state.links);
  const [alert, setAlert] = useState<AlertRecord | null>(null);
  const [newStatus, setNewStatus] = useState<AlertStatus>('UnderReview');
  const [analystName, setAnalystName] = useState('');
  const [comment, setComment] = useState('');
  const [isSaving, setIsSaving] = useState(false);
  const [investigationGraph, setInvestigationGraph] = useState<EntityGraph>({ nodes: [], edges: [] });

  useEffect(() => {
    if (!alertId) {
      setAlert(null);
      setInvestigationGraph({ nodes: [], edges: [] });
      return;
    }

    const abortController = new AbortController();
    Promise.all([
      amlApi.getAlertById(alertId, abortController.signal),
      amlApi.getGraph({ limit: 5000 }, abortController.signal),
    ])
      .then(([alertData, graphData]) => {
        setAlert(alertData);
        setNewStatus(alertData.status);
        setInvestigationGraph(graphData);
      })
      .catch(() => {
        setAlert(null);
        setInvestigationGraph({ nodes: [], edges: [] });
      });

    return () => abortController.abort();
  }, [alertId]);

  const involvedCustomerAccounts = useMemo(
    () => alert
      ? mapInvolvedCustomerAccounts(
        alert.involvedAccountIds,
        mergeNodes(graphNodes, investigationGraph.nodes),
        mergeLinks(graphLinks, investigationGraph.edges),
      )
      : [],
    [alert, graphLinks, graphNodes, investigationGraph.edges, investigationGraph.nodes],
  );

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
                <dt>Status</dt>
                <dd>{alert.status}</dd>
              </div>
              <div>
                <dt>Detected At</dt>
                <dd>{formatDateTime(alert.detectedAt)}</dd>
              </div>
              <div className="alert-detail-grid__wide">
                <dt>Involved Customers</dt>
                <dd>
                  {involvedCustomerAccounts.length > 0 ? (
                    <ul className="involved-customers-list">
                      {involvedCustomerAccounts.map((item) => (
                        <li key={item.accountId}>
                          <strong>{item.clientName}</strong>
                          <span>{item.iban}</span>
                          <small>Risk {item.riskScore}</small>
                        </li>
                      ))}
                    </ul>
                  ) : 'Customer context is loading from the graph.'}
                </dd>
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

function mapInvolvedCustomerAccounts(
  involvedAccountIds: string[],
  graphNodes: EntityGraphNode[],
  graphLinks: EntityGraphEdge[],
): InvolvedCustomerAccount[] {
  const nodesByKey = new Map<string, EntityGraphNode>();
  graphNodes.forEach((node) => {
    getNodeKeys(node).forEach((key) => nodesByKey.set(key, node));
  });

  const ownerByAccountKey = new Map<string, EntityGraphNode>();
  graphLinks
    .filter((link) => link.type === 'OWNS')
    .forEach((link) => {
      const client = nodesByKey.get(link.sourceId);
      const account = nodesByKey.get(link.targetId);
      if (!client || !account) {
        return;
      }

      getNodeKeys(account).forEach((key) => ownerByAccountKey.set(key, client));
  });

  return involvedAccountIds.map((accountKey) => {
    const account = nodesByKey.get(accountKey);
    const owner = account
      ? getNodeKeys(account)
        .map((key) => ownerByAccountKey.get(key))
        .find((candidate): candidate is EntityGraphNode => Boolean(candidate))
      : undefined;

    const iban = account ? stringValue(account.properties.IBAN) ?? accountKey : accountKey;
    const clientName = owner
      ? stringValue(owner.properties.Name) ?? `Customer owning ${iban}`
      : `Customer owning ${iban}`;

    return {
      accountId: stringValue(account?.properties.Id) ?? account?.id ?? accountKey,
      iban,
      clientName,
      riskScore: owner ? numberValue(owner.properties.RiskScore) : 0,
    };
  });
}

function mergeNodes(primary: EntityGraphNode[], secondary: EntityGraphNode[]): EntityGraphNode[] {
  const byKey = new Map<string, EntityGraphNode>();
  [...secondary, ...primary].forEach((node) => {
    const stableKey = stringValue(node.properties.Id) ?? node.id;
    byKey.set(stableKey, node);
  });

  return [...byKey.values()];
}

function mergeLinks(primary: EntityGraphEdge[], secondary: EntityGraphEdge[]): EntityGraphEdge[] {
  const byKey = new Map<string, EntityGraphEdge>();
  [...secondary, ...primary].forEach((link) => {
    byKey.set(link.id, link);
  });

  return [...byKey.values()];
}

function getNodeKeys(node: EntityGraphNode): string[] {
  return [
    node.id,
    stringValue(node.properties.Id),
    stringValue(node.properties.IBAN),
  ].filter((value): value is string => Boolean(value));
}

function stringValue(value: unknown): string | undefined {
  return typeof value === 'string' && value.length > 0 ? value : undefined;
}

function numberValue(value: unknown): number {
  return typeof value === 'number' && Number.isFinite(value) ? value : 0;
}
