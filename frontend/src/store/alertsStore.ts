import { create } from 'zustand';
import type { AlertSeverityFilter, AmlAlert } from '../api/types';

interface AlertsState {
  alerts: AmlAlert[];
  unreadCount: number;
  selectedAlertId: string | null;
  severityFilter: AlertSeverityFilter;
  isRealtimeConnected: boolean;
  isLoading: boolean;
  setAlerts: (alerts: AmlAlert[]) => void;
  addAlert: (alert: AmlAlert) => void;
  markRead: () => void;
  setFilter: (filter: AlertSeverityFilter) => void;
  selectAlert: (alertId: string | null) => void;
  setRealtimeConnected: (isConnected: boolean) => void;
  setLoading: (isLoading: boolean) => void;
}

export const useAlertsStore = create<AlertsState>((set) => ({
  alerts: [],
  unreadCount: 0,
  selectedAlertId: null,
  severityFilter: 'All',
  isRealtimeConnected: false,
  isLoading: true,
  setAlerts: (alerts) => set({ alerts, isLoading: false }),
  addAlert: (alert) =>
    set((state) => {
      const existingIndex = state.alerts.findIndex((existingAlert) => existingAlert.id === alert.id);
      const alerts = existingIndex >= 0
        ? state.alerts.map((existingAlert) => (existingAlert.id === alert.id ? alert : existingAlert))
        : [alert, ...state.alerts];

      return {
        alerts: alerts.slice(0, 250),
        unreadCount: existingIndex >= 0 ? state.unreadCount : state.unreadCount + 1,
      };
    }),
  markRead: () => set({ unreadCount: 0 }),
  setFilter: (severityFilter) => set({ severityFilter }),
  selectAlert: (selectedAlertId) => set({ selectedAlertId, unreadCount: 0 }),
  setRealtimeConnected: (isRealtimeConnected) => set({ isRealtimeConnected }),
  setLoading: (isLoading) => set({ isLoading }),
}));
