import { useCallback, useEffect, useState } from "react";
import { ApiError } from "@/api/client";

export function describe(error: unknown): string {
  return error instanceof ApiError ? error.describe() : "Something went wrong. Try again.";
}

/** Loads data on mount and whenever deps change. Returns a reload function for after mutations. */
export function useLoad<T>(load: () => Promise<T>, deps: unknown[]) {
  const [data, setData] = useState<T | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [status, setStatus] = useState<number | null>(null);

  const run = useCallback(load, deps);

  const reload = useCallback(async () => {
    try {
      setData(await run());
      setError(null);
    } catch (e) {
      setError(describe(e));
      setStatus(e instanceof ApiError ? e.status : null);
    }
  }, [run]);

  useEffect(() => {
    setData(null);
    void reload();
  }, [reload]);

  return { data, setData, error, status, reload };
}

/** Wraps a mutation with busy and error state. */
export function useAction() {
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const run = useCallback(async (action: () => Promise<void>) => {
    setBusy(true);
    setError(null);
    try {
      await action();
      return true;
    } catch (e) {
      setError(describe(e));
      return false;
    } finally {
      setBusy(false);
    }
  }, []);

  return { busy, error, setError, run };
}
