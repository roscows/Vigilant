import { create } from 'zustand';
import type { AlertRecord, AlertSeverityFilter, AlertStatusFilter, AmlAlert } from '../api/types';

export interface AccountAlertFocus {
  ibanQuery: string;
  accountIds: string[];
  sourceLabel: string;
}

interface AlertsState {
  alerts: AlertRecord[];
  unreadCount: number;
  selectedAlertId: string | null;
  severityFilter: AlertSeverityFilter;
  statusFilter: AlertStatusFilter;
  accountFocus: AccountAlertFocus | null;
  isRealtimeConnected: boolean;
  isLoading: boolean;
  recentAlertIds: string[];
  setAlerts: (alerts: AlertRecord[]) => void;
  addDetectedAlert: (alert: AmlAlert) => void;
  upsertAlert: (alert: AlertRecord, markUnread?: boolean) => void;
  markRead: () => void;
  setFilter: (filter: AlertSeverityFilter) => void;
  setStatusFilter: (filter: AlertStatusFilter) => void;
  setAccountFocus: (focus: AccountAlertFocus | null) => void;
  selectAlert: (alertId: string | null) => void;
  setRealtimeConnected: (isConnected: boolean) => void;
  setLoading: (isLoading: boolean) => void;
}

export const useAlertsStore = create<AlertsState>((set) => ({
  alerts: [],
  unreadCount: 0,
  selectedAlertId: null,
  severityFilter: 'All',
  statusFilter: 'All',
  accountFocus: null,
  isRealtimeConnected: false,
  isLoading: true,
  recentAlertIds: [],
  setAlerts: (alerts) => set({ alerts, isLoading: false }),
  addDetectedAlert: (alert) =>
    set((state) => upsertAlertState(state, fromDetectedAlert(alert), true)),
  upsertAlert: (alert, markUnread = false) =>
    set((state) => upsertAlertState(state, alert, markUnread)),
  markRead: () => set({ unreadCount: 0, recentAlertIds: [] }),
  setFilter: (severityFilter) => set({ severityFilter }),
  setStatusFilter: (statusFilter) => set({ statusFilter }),
  setAccountFocus: (accountFocus) => set({ accountFocus }),
  selectAlert: (selectedAlertId) => set({ selectedAlertId, unreadCount: 0 }),
  setRealtimeConnected: (isRealtimeConnected) => set({ isRealtimeConnected }),
  setLoading: (isLoading) => set({ isLoading }),
}));

type AlertsStateSnapshot = Pick<AlertsState, 'alerts' | 'unreadCount' | 'recentAlertIds'>;

function upsertAlertState(state: AlertsStateSnapshot, alert: AlertRecord, markUnread: boolean): Partial<AlertsState> {
  const existingIndex = state.alerts.findIndex((existingAlert) => existingAlert.id === alert.id);
  const alerts = existingIndex >= 0
    ? state.alerts.map((existingAlert) => (existingAlert.id === alert.id ? alert : existingAlert))
    : [alert, ...state.alerts];

  return {
    alerts: alerts.slice(0, 500),
    unreadCount: existingIndex >= 0 || !markUnread ? state.unreadCount : state.unreadCount + 1,
    recentAlertIds: markUnread ? [alert.id, ...state.recentAlertIds.filter((id) => id !== alert.id)].slice(0, 16) : state.recentAlertIds,
  };
}

function fromDetectedAlert(alert: AmlAlert): AlertRecord {
  return {
    id: alert.id,
    ruleType: alert.type,
    severity: alert.severity,
    status: 'New',
    involvedAccountIds: alert.accountIds,
    detectedAt: alert.detectedAtUtc,
    auditLog: [],
    message: alert.message,
  };
}
