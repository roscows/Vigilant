export type AlertSeverity = 'Medium' | 'High' | 'Critical';
export type AlertSeverityFilter = 'All' | AlertSeverity;

export interface AmlAlert {
  id: string;
  type: string;
  severity: AlertSeverity;
  message: string;
  accountIban: string;
  totalAmount: number;
  transactionIds: string[];
  accountIbans: string[];
  clientIds: string[];
  deviceIds: string[];
  ipAddresses: string[];
  involvedNodeKeys: string[];
  detectedAtUtc: string;
}

export interface EntityGraphNode {
  id: string;
  label: 'Client' | 'Account' | 'Transaction' | 'IpAddress' | 'Device' | string;
  labels: string[];
  properties: Record<string, unknown>;
}

export interface EntityGraphEdge {
  id: string;
  sourceId: string;
  targetId: string;
  type: string;
  properties: Record<string, unknown>;
}

export interface EntityGraph {
  nodes: EntityGraphNode[];
  edges: EntityGraphEdge[];
}

export interface ClientSnapshot {
  id: string;
  name: string;
  riskScore: number;
  isPep?: boolean;
}

export interface ProcessTransactionPayload {
  senderIban: string;
  receiverIban: string;
  amount: number;
  currency: string;
  deviceId: string;
  ipAddress: string;
  ipCountryCode?: string;
  browserFingerprint?: string;
  senderAccountCountryCode?: string;
  receiverAccountCountryCode?: string;
  senderClient?: ClientSnapshot;
  receiverClient?: ClientSnapshot;
}

export interface ProcessTransactionResult {
  transactionId: string;
  senderIban: string;
  receiverIban: string;
  amount: number;
  currency: string;
  processedAtUtc: string;
  triggeredAlerts: AmlAlert[];
}

export interface SeedTransactionsPayload {
  clientCount?: number;
  accountCount?: number;
  randomTransactionCount?: number;
  circularFlowCount?: number;
}

export interface SeedTransactionsResult {
  clientsCreated: number;
  accountsCreated: number;
  transactionsCreated: number;
  circularFlowsCreated: number;
  focusAccountIban: string;
  circularAccountIbans: string[];
  transactionIds: string[];
  triggeredAlerts: AmlAlert[];
}

export interface RiskContribution {
  alertType: string;
  description: string;
  weight: number;
}

export interface ClientRiskScore {
  clientId: string;
  riskScore: number;
  contributingAlerts: RiskContribution[];
}
