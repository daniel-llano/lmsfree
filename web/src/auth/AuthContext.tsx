import { createContext, useCallback, useContext, useEffect, useMemo, useState, type ReactNode } from "react";
import { readSession, writeSession } from "@/api/client";
import { authApi } from "@/api/endpoints";
import type { AuthResponse, User } from "@/api/types";

interface AuthState {
  user: User | null;
  loading: boolean;
  isAdmin: boolean;
  isStaff: boolean;
  login: (email: string, password: string) => Promise<void>;
  register: (email: string, password: string, displayName: string) => Promise<void>;
  logout: () => void;
}

const AuthContext = createContext<AuthState | null>(null);

export function AuthProvider({ children }: { children: ReactNode }) {
  const [user, setUser] = useState<User | null>(null);
  const [loading, setLoading] = useState(() => readSession() !== null);

  useEffect(() => {
    if (!readSession()) return;
    authApi
      .me()
      .then(setUser)
      .catch(() => writeSession(null))
      .finally(() => setLoading(false));
  }, []);

  const accept = useCallback((auth: AuthResponse) => {
    writeSession({ token: auth.token, expiresAt: auth.expiresAt });
    setUser(auth.user);
  }, []);

  const value = useMemo<AuthState>(
    () => ({
      user,
      loading,
      isAdmin: user?.role === "Admin",
      isStaff: user?.role === "Admin" || user?.role === "Teacher",
      login: async (email, password) => accept(await authApi.login(email, password)),
      register: async (email, password, displayName) => accept(await authApi.register(email, password, displayName)),
      logout: () => {
        writeSession(null);
        setUser(null);
      },
    }),
    [user, loading, accept],
  );

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}

export function useAuth(): AuthState {
  const ctx = useContext(AuthContext);
  if (!ctx) throw new Error("useAuth must be used inside <AuthProvider>.");
  return ctx;
}
