import { useCallback, useEffect, useMemo, useState } from 'react';
import { amlApi } from '../api/amlApi';
import type { AlertQuery, AlertRecord } from '../api/types';

const defaultAlertQuery: AlertQuery = {};

export function useAlerts(initialQuery: AlertQuery = defaultAlertQuery) {
  const query = useMemo(() => initialQuery, [initialQuery]);
  const [alerts, setAlerts] = useState<AlertRecord[]>([]);
  const [isLoading, setIsLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const loadAlerts = useCallback(async (signal?: AbortSignal) => {
    setIsLoading(true);
    setError(null);

    try {
      const data = await amlApi.getAlerts(query, signal);
      setAlerts(data);
    } catch (error) {
      if (signal?.aborted) {
        return;
      }

      setError(error instanceof Error ? error.message : 'Unable to load AML alerts.');
    } finally {
      if (!signal?.aborted) {
        setIsLoading(false);
      }
    }
  }, [query]);

  useEffect(() => {
    const abortController = new AbortController();
    void loadAlerts(abortController.signal);

    return () => abortController.abort();
  }, [loadAlerts]);

  return { alerts, setAlerts, isLoading, error, refresh: loadAlerts };
}

