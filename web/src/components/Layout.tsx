import type { ReactNode } from "react";
import { Link, NavLink, Navigate, Outlet, useLocation, useNavigate } from "react-router";
import { useAuth } from "@/auth/AuthContext";

export function Layout() {
  const { user, isAdmin, logout } = useAuth();
  const navigate = useNavigate();

  return (
    <>
      <header className="app-header">
        <nav className="container d-flex flex-wrap align-items-center gap-3 py-2">
          <Link to="/" className="brand">
            LMSFree
          </Link>
          {user && (
            <>
              <NavLink to="/" end className="nav-link">
                Courses
              </NavLink>
              {user.role === "Student" && (
                <NavLink to="/grades" className="nav-link">
                  My grades
                </NavLink>
              )}
              {isAdmin && (
                <NavLink to="/admin/users" className="nav-link">
                  Users
                </NavLink>
              )}
            </>
          )}
          <div className="ms-auto d-flex align-items-center gap-2">
            {user ? (
              <>
                <NavLink to="/account" className="nav-link small">
                  {user.displayName} <span className="badge text-bg-light ms-1">{user.role}</span>
                </NavLink>
                <button
                  className="btn btn-sm btn-outline-light"
                  onClick={() => {
                    logout();
                    navigate("/login");
                  }}
                >
                  Sign out
                </button>
              </>
            ) : (
              <>
                <Link to="/login" className="btn btn-sm btn-light">
                  Sign in
                </Link>
                <Link to="/register" className="btn btn-sm btn-outline-light">
                  Create account
                </Link>
              </>
            )}
          </div>
        </nav>
      </header>
      <main className="container py-4">
        <Outlet />
      </main>
    </>
  );
}

export function RequireAuth({ children, roles }: { children: ReactNode; roles?: string[] }) {
  const { user, loading } = useAuth();
  const location = useLocation();
  if (loading) return <p className="text-muted">Checking your session…</p>;
  if (!user) return <Navigate to={`/login?returnTo=${encodeURIComponent(location.pathname + location.search)}`} replace />;
  if (roles && !roles.includes(user.role)) return <div className="alert alert-warning">You don't have access to this page.</div>;
  return <>{children}</>;
}

export function Alert({ error }: { error: string | null }) {
  return error ? (
    <div className="alert alert-danger" role="alert">
      {error}
    </div>
  ) : null;
}

export function formatDue(value: string | null) {
  return value ? `Due ${formatDate(value)}` : "No due date";
}

export function formatDate(value: string | null, withTime = true) {
  if (!value) return "";
  return new Date(value).toLocaleString(undefined, withTime ? { dateStyle: "medium", timeStyle: "short" } : { dateStyle: "medium" });
}
