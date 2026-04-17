import { create } from 'zustand';
import type { AmlAlert } from '../api/types';

interface AlertsState {
  alerts: AmlAlert[];
  isRealtimeConnected: boolean;
  setAlerts: (alerts: AmlAlert[]) => void;
  prependAlerts: (alerts: AmlAlert[]) => void;
  setRealtimeConnected: (isConnected: boolean) => void;
}

export const useAlertsStore = create<AlertsState>((set) => ({
  alerts: [],
  isRealtimeConnected: false,
  setAlerts: (alerts) => set({ alerts }),
  prependAlerts: (alerts) =>
    set((state) => ({
      alerts: [...alerts, ...state.alerts].slice(0, 100),
    })),
  setRealtimeConnected: (isRealtimeConnected) => set({ isRealtimeConnected }),
}));

