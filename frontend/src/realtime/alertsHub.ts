import { HubConnection, HubConnectionBuilder, LogLevel } from '@microsoft/signalr';
import { apiBaseUrl } from '../api/httpClient';
import type { AmlAlert } from '../api/types';

let connection: HubConnection | null = null;

export async function startAlertsHub(onAlertsDetected: (alerts: AmlAlert[]) => void): Promise<HubConnection> {
  if (connection?.state === 'Connected') {
    return connection;
  }

  connection = new HubConnectionBuilder()
    .withUrl(`${apiBaseUrl}/hubs/alerts`)
    .withAutomaticReconnect()
    .configureLogging(LogLevel.Information)
    .build();

  connection.on('alerts.detected', onAlertsDetected);
  await connection.start();
  await connection.invoke('SubscribeToAlerts');

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

