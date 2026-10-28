import { useState, type FormEvent } from "react";
import { Link, useNavigate, useSearchParams } from "react-router";
import { ApiError } from "@/api/client";
import { authApi } from "@/api/endpoints";
import { useAuth } from "@/auth/AuthContext";
import { Alert } from "@/components/Layout";
import { useAction } from "@/components/useAsync";

function useReturnTo() {
  const [params] = useSearchParams();
  const target = params.get("returnTo") ?? "/";
  return target.startsWith("/") && !target.startsWith("//") ? target : "/";
}

function Card({ title, children }: { title: string; children: React.ReactNode }) {
  return (
    <div className="card shadow-sm card-narrow">
      <div className="card-body p-4">
        <h1 className="h4 mb-3">{title}</h1>
        {children}
      </div>
    </div>
  );
}

export function LoginPage() {
  const { login } = useAuth();
  const navigate = useNavigate();
  const returnTo = useReturnTo();
  const [params] = useSearchParams();
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const { busy, error, setError, run } = useAction();

  const submit = async (e: FormEvent) => {
    e.preventDefault();
    await run(async () => {
      try {
        await login(email, password);
        navigate(returnTo, { replace: true });
      } catch (err) {
        if (err instanceof ApiError && err.status === 401) setError("Wrong email or password, or the account is deactivated.");
        else throw err;
      }
    });
  };

  return (
    <Card title="Sign in">
      {params.get("reset") && !error && <div className="alert alert-success">Password updated. Sign in with your new password.</div>}
      <Alert error={error} />
      <form onSubmit={submit}>
        <div className="mb-3">
          <label className="form-label" htmlFor="email">Email</label>
          <input id="email" type="email" className="form-control" autoComplete="email" required value={email} onChange={(e) => setEmail(e.target.value)} />
        </div>
        <div className="mb-2">
          <label className="form-label" htmlFor="password">Password</label>
          <input id="password" type="password" className="form-control" autoComplete="current-password" required value={password} onChange={(e) => setPassword(e.target.value)} />
        </div>
        <div className="mb-3 text-end small">
          <Link to="/forgot-password">Forgot your password?</Link>
        </div>
        <button className="btn btn-primary w-100" disabled={busy}>{busy ? "Signing in…" : "Sign in"}</button>
      </form>
      <p className="text-center small mt-3 mb-0">
        New student? <Link to="/register">Create an account</Link>
      </p>
    </Card>
  );
}

export function RegisterPage() {
  const { register } = useAuth();
  const navigate = useNavigate();
  const [form, setForm] = useState({ displayName: "", email: "", password: "" });
  const { busy, error, run } = useAction();

  const submit = async (e: FormEvent) => {
    e.preventDefault();
    if (await run(() => register(form.email, form.password, form.displayName))) navigate("/", { replace: true });
  };

  return (
    <Card title="Create a student account">
      <p className="text-muted small">Your teacher enrolls you in courses once you have an account.</p>
      <Alert error={error} />
      <form onSubmit={submit}>
        <div className="mb-3">
          <label className="form-label" htmlFor="name">Full name</label>
          <input id="name" className="form-control" required minLength={2} maxLength={100} value={form.displayName} onChange={(e) => setForm({ ...form, displayName: e.target.value })} />
        </div>
        <div className="mb-3">
          <label className="form-label" htmlFor="email">Email</label>
          <input id="email" type="email" className="form-control" autoComplete="email" required value={form.email} onChange={(e) => setForm({ ...form, email: e.target.value })} />
        </div>
        <div className="mb-3">
          <label className="form-label" htmlFor="password">Password</label>
          <input id="password" type="password" className="form-control" autoComplete="new-password" required minLength={8} value={form.password} onChange={(e) => setForm({ ...form, password: e.target.value })} />
          <div className="form-text">At least 8 characters, with a letter and a digit.</div>
        </div>
        <button className="btn btn-primary w-100" disabled={busy}>{busy ? "Creating account…" : "Create account"}</button>
      </form>
      <p className="text-center small mt-3 mb-0">
        Already registered? <Link to="/login">Sign in</Link>
      </p>
    </Card>
  );
}

export function ForgotPasswordPage() {
  const [email, setEmail] = useState("");
  const [sent, setSent] = useState(false);
  const { busy, error, run } = useAction();

  const submit = async (e: FormEvent) => {
    e.preventDefault();
    if (await run(() => authApi.forgot(email))) setSent(true);
  };

  return (
    <Card title="Reset your password">
      {sent ? (
        <div className="alert alert-success">If {email} has an account, a reset link is on its way. It works for one hour.</div>
      ) : (
        <>
          <Alert error={error} />
          <form onSubmit={submit}>
            <div className="mb-3">
              <label className="form-label" htmlFor="email">Email</label>
              <input id="email" type="email" className="form-control" required value={email} onChange={(e) => setEmail(e.target.value)} />
            </div>
            <button className="btn btn-primary w-100" disabled={busy}>Send reset link</button>
          </form>
        </>
      )}
      <p className="text-center small mt-3 mb-0">
        <Link to="/login">Back to sign in</Link>
      </p>
    </Card>
  );
}

export function ResetPasswordPage() {
  const [params] = useSearchParams();
  const navigate = useNavigate();
  const [password, setPassword] = useState("");
  const { busy, error, run } = useAction();
  const token = params.get("token") ?? "";

  const submit = async (e: FormEvent) => {
    e.preventDefault();
    if (await run(() => authApi.reset(token, password))) navigate("/login?reset=1", { replace: true });
  };

  return (
    <Card title="Choose a new password">
      {!token && <div className="alert alert-warning">This link is missing its token. Request a new one.</div>}
      <Alert error={error} />
      <form onSubmit={submit}>
        <div className="mb-3">
          <label className="form-label" htmlFor="password">New password</label>
          <input id="password" type="password" className="form-control" autoComplete="new-password" required minLength={8} value={password} onChange={(e) => setPassword(e.target.value)} />
        </div>
        <button className="btn btn-primary w-100" disabled={busy || !token}>Save password</button>
      </form>
    </Card>
  );
}

export function AccountPage() {
  const { user } = useAuth();
  const [form, setForm] = useState({ current: "", next: "" });
  const [saved, setSaved] = useState(false);
  const { busy, error, run } = useAction();

  const submit = async (e: FormEvent) => {
    e.preventDefault();
    setSaved(false);
    if (await run(() => authApi.changePassword(form.current, form.next))) {
      setSaved(true);
      setForm({ current: "", next: "" });
    }
  };

  return (
    <div style={{ maxWidth: "28rem" }}>
      <h1 className="h4">Your account</h1>
      <p className="text-muted">{user?.email} · {user?.role}</p>
      <h2 className="h6 mt-4">Change password</h2>
      {saved && <div className="alert alert-success">Password updated.</div>}
      <Alert error={error} />
      <form onSubmit={submit}>
        <div className="mb-3">
          <label className="form-label" htmlFor="current">Current password</label>
          <input id="current" type="password" className="form-control" required value={form.current} onChange={(e) => setForm({ ...form, current: e.target.value })} />
        </div>
        <div className="mb-3">
          <label className="form-label" htmlFor="next">New password</label>
          <input id="next" type="password" className="form-control" required minLength={8} value={form.next} onChange={(e) => setForm({ ...form, next: e.target.value })} />
        </div>
        <button className="btn btn-primary" disabled={busy}>Update password</button>
      </form>
    </div>
  );
}
