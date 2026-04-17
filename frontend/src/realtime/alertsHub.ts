import { HubConnection, HubConnectionBuilder, HubConnectionState, LogLevel } from '@microsoft/signalr';
import { apiBaseUrl } from '../api/httpClient';
import type { AmlAlert } from '../api/types';

let connection: HubConnection | null = null;

interface AlertsHubHandlers {
  onAlert: (alert: AmlAlert) => void;
  onConnectionChange: (isConnected: boolean) => void;
}

export async function startAlertsHub(handlers: AlertsHubHandlers): Promise<HubConnection> {
  if (connection?.state === HubConnectionState.Connected) {
    handlers.onConnectionChange(true);
    return connection;
  }

  connection = new HubConnectionBuilder()
    .withUrl(`${apiBaseUrl}/hubs/alerts`)
    .withAutomaticReconnect()
    .configureLogging(LogLevel.Warning)
    .build();

  connection.on('alerts.detected', (alerts: AmlAlert[]) => {
    for (const alert of alerts) {
      handlers.onAlert(alert);
    }
  });

  connection.onreconnecting(() => handlers.onConnectionChange(false));
  connection.onreconnected(() => handlers.onConnectionChange(true));
  connection.onclose(() => handlers.onConnectionChange(false));

  await connection.start();
  await connection.invoke('SubscribeToAlerts');
  handlers.onConnectionChange(true);

  return connection;
}

export async function stopAlertsHub(): Promise<void> {
  if (!connection) {
    return;
  }

  await connection.invoke('UnsubscribeFromAlerts').catch(() => undefined);
  await connection.stop();
  connection = null;
}
