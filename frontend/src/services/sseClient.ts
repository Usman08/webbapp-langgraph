const BASE_URL = import.meta.env.VITE_API_URL ?? "";

export type SseEventHandler = (event: MessageEvent) => void;

export function connectSse(
  path: string,
  onMessage: SseEventHandler,
  onError?: (e: Event) => void
): EventSource {
  const es = new EventSource(`${BASE_URL}${path}`);
  es.onmessage = onMessage;
  if (onError) es.onerror = onError;
  return es;
}
