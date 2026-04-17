import { httpClient } from './httpClient';
import type {
  AlertQuery,
  AlertRecord,
  ClientRiskScore,
  EntityGraph,
  GraphQuery,
  ProcessTransactionPayload,
  ProcessTransactionResult,
  SeedTransactionsPayload,
  SeedTransactionsResult,
  UpdateAlertStatusPayload,
} from './types';

export const amlApi = {
  async getAlerts(query: AlertQuery = {}, signal?: AbortSignal): Promise<AlertRecord[]> {
    const { data } = await httpClient.get<AlertRecord[]>('/api/alerts', { params: query, signal });
    return data;
  },

  async getAlertById(alertId: string, signal?: AbortSignal): Promise<AlertRecord> {
    const { data } = await httpClient.get<AlertRecord>(`/api/alerts/${encodeURIComponent(alertId)}`, { signal });
    return data;
  },

  async updateAlertStatus(alertId: string, payload: UpdateAlertStatusPayload): Promise<AlertRecord> {
    const { data } = await httpClient.patch<AlertRecord>(`/api/alerts/${encodeURIComponent(alertId)}/status`, payload);
    return data;
  },

  async getGraph(query: GraphQuery = {}, signal?: AbortSignal): Promise<EntityGraph> {
    const { data } = await httpClient.get<EntityGraph>('/api/graph', { params: query, signal });
    return data;
  },

  async getEntityGraph(accountIban: string, depth = 5, signal?: AbortSignal): Promise<EntityGraph> {
    const { data } = await httpClient.get<EntityGraph>(
      `/api/graph/accounts/${encodeURIComponent(accountIban)}`,
      { params: { depth }, signal },
    );
    return data;
  },

  async getGraphOverview(nodeLimit = 250, signal?: AbortSignal): Promise<EntityGraph> {
    const { data } = await httpClient.get<EntityGraph>('/api/graph/overview', {
      params: { nodeLimit },
      signal,
    });
    return data;
  },

  async getClientRisk(clientId: string, signal?: AbortSignal): Promise<ClientRiskScore> {
    const { data } = await httpClient.get<ClientRiskScore>(`/api/clients/${encodeURIComponent(clientId)}/risk`, { signal });
    return data;
  },

  async processTransaction(payload: ProcessTransactionPayload): Promise<ProcessTransactionResult> {
    const { data } = await httpClient.post<ProcessTransactionResult>('/api/transactions/process', payload);
    return data;
  },

  async seedTransactions(payload: SeedTransactionsPayload): Promise<SeedTransactionsResult> {
    const { data } = await httpClient.post<SeedTransactionsResult>('/api/transactions/seed', payload);
    return data;
  },
};