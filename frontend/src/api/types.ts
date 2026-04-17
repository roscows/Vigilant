export type AlertSeverity = 'Medium' | 'High' | 'Critical';

export interface AmlAlert {
  id: string;
  type: string;
  severity: AlertSeverity;
  message: string;
  accountIban: string;
  totalAmount: number;
  transactionIds: string[];
  accountIbans: string[];
  detectedAtUtc: string;
}

export interface EntityGraphNode {
  id: string;
  label: string;
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

export interface ProcessTransactionPayload {
  senderIban: string;
  receiverIban: string;
  amount: number;
  currency: string;
  deviceId: string;
  ipAddress: string;
  ipCountryCode?: string;
  browserFingerprint?: string;
  senderClient?: ClientSnapshot;
  receiverClient?: ClientSnapshot;
}

export interface ClientSnapshot {
  id: string;
  name: string;
  riskScore: number;
}

export interface ProcessTransactionResult {
  transactionId: string;
  senderIban: string;
  receiverIban: string;
  amount: number;
  currency: string;
  processedAtUtc: string;
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

