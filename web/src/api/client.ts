const baseUrl = (import.meta.env.VITE_API_URL ?? "").replace(/\/$/, "");
const tokenKey = "lmsfree.session";

export class ApiError extends Error {
  readonly status: number;
  readonly fieldErrors: Record<string, string[]>;

  constructor(status: number, message: string, fieldErrors: Record<string, string[]> = {}) {
    super(message);
    this.status = status;
    this.fieldErrors = fieldErrors;
  }

  /** The message plus any field errors, ready for an alert. */
  describe(): string {
    const fields = Object.values(this.fieldErrors).flat();
    return fields.length ? fields.join(" ") : this.message;
  }
}

export interface StoredSession {
  token: string;
  expiresAt: string;
}

export function readSession(): StoredSession | null {
  try {
    const raw = localStorage.getItem(tokenKey);
    if (!raw) return null;
    const session = JSON.parse(raw) as StoredSession;
    return new Date(session.expiresAt).getTime() > Date.now() ? session : null;
  } catch {
    return null;
  }
}

export function writeSession(session: StoredSession | null) {
  if (session) localStorage.setItem(tokenKey, JSON.stringify(session));
  else localStorage.removeItem(tokenKey);
}

const fallbackMessages: Record<number, string> = {
  401: "Sign in to do that.",
  403: "You don't have permission to do that.",
  404: "That item doesn't exist or you don't have access to it.",
  429: "Too many attempts. Wait a minute and try again.",
};

export async function request<T>(path: string, init: RequestInit = {}): Promise<T> {
  const headers = new Headers(init.headers);
  headers.set("Accept", "application/json");
  if (typeof init.body === "string") headers.set("Content-Type", "application/json");
  const session = readSession();
  if (session) headers.set("Authorization", `Bearer ${session.token}`);

  let response: Response;
  try {
    response = await fetch(`${baseUrl}${path}`, { ...init, headers });
  } catch {
    throw new ApiError(0, "Can't reach the server. Check your connection and try again.");
  }

  if (!response.ok) {
    let title: string | undefined;
    let errors: Record<string, string[]> = {};
    try {
      const problem = await response.json();
      title = problem.detail ?? problem.title;
      errors = problem.errors ?? {};
    } catch {
      // Not a problem+json body. Fall back to a generic message.
    }
    const message = response.status === 429 ? fallbackMessages[429] : (title ?? fallbackMessages[response.status] ?? `Request failed (${response.status}).`);
    throw new ApiError(response.status, message, errors);
  }

  const body = await response.text();
  return (body ? JSON.parse(body) : undefined) as T;
}
