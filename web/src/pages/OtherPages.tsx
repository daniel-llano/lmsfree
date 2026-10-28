import { useState, type FormEvent } from "react";
import { Link } from "react-router";
import { assignmentsApi, usersApi } from "@/api/endpoints";
import type { Role, User } from "@/api/types";
import { useAuth } from "@/auth/AuthContext";
import { Alert, formatDate } from "@/components/Layout";
import { useAction, useLoad } from "@/components/useAsync";

export function GradesPage() {
  const { data: grades, error } = useLoad(() => assignmentsApi.myGrades(), []);

  return (
    <>
      <h1 className="h3 mb-3">My grades</h1>
      <Alert error={error} />
      {grades?.length === 0 && <p className="text-muted">Nothing to show yet.</p>}
      {grades && grades.length > 0 && (
        <table className="table bg-white shadow-sm align-middle">
          <thead className="table-light">
            <tr><th>Course</th><th>Assignment</th><th>Submitted</th><th>Score</th><th>Feedback</th></tr>
          </thead>
          <tbody>
            {grades.map((g) => (
              <tr key={g.assignmentId}>
                <td>{g.courseTitle}</td>
                <td><Link to={`/assignments/${g.assignmentId}`}>{g.assignmentTitle}</Link></td>
                <td>{g.submittedAt ? formatDate(g.submittedAt) : <span className="text-warning">Not submitted</span>}</td>
                <td>{g.score != null ? `${g.score} / ${g.maxPoints}` : "Not graded"}</td>
                <td className="small">{g.feedback}</td>
              </tr>
            ))}
          </tbody>
        </table>
      )}
    </>
  );
}

const roles: Role[] = ["Student", "Teacher", "Admin"];

export function UsersPage() {
  const { user: me } = useAuth();
  const [filter, setFilter] = useState<Role | "">("");
  const { data: users, setData, error } = useLoad(() => usersApi.list(filter || undefined), [filter]);
  const action = useAction();

  const save = (u: User, changes: Partial<User>) =>
    action.run(async () => {
      const next = { ...u, ...changes };
      const updated = await usersApi.update(u.id, { displayName: next.displayName, role: next.role, isActive: next.isActive });
      setData((users ?? []).map((x) => (x.id === updated.id ? updated : x)));
    });

  return (
    <>
      <div className="page-title">
        <h1>Users</h1>
        <select className="form-select w-auto" aria-label="Filter by role" value={filter} onChange={(e) => setFilter(e.target.value as Role | "")}>
          <option value="">All roles</option>
          {roles.map((r) => <option key={r} value={r}>{r}s</option>)}
        </select>
      </div>
      <CreateUser onCreated={(u) => setData([u, ...(users ?? [])])} />
      <Alert error={action.error ?? error} />
      <table className="table bg-white shadow-sm align-middle">
        <thead className="table-light">
          <tr><th>Name</th><th>Email</th><th>Role</th><th>Status</th><th>Joined</th></tr>
        </thead>
        <tbody>
          {users?.map((u) => (
            <tr key={u.id} className={u.isActive ? "" : "text-muted"}>
              <td>{u.displayName}{u.id === me?.id && <span className="badge text-bg-info ms-1">you</span>}</td>
              <td>{u.email}</td>
              <td style={{ maxWidth: "9rem" }}>
                <select className="form-select form-select-sm" aria-label={`Role for ${u.displayName}`} value={u.role} disabled={u.id === me?.id || action.busy} onChange={(e) => save(u, { role: e.target.value as Role })}>
                  {roles.map((r) => <option key={r}>{r}</option>)}
                </select>
              </td>
              <td>
                <button className={`btn btn-sm ${u.isActive ? "btn-outline-secondary" : "btn-outline-success"}`} disabled={u.id === me?.id || action.busy} onClick={() => save(u, { isActive: !u.isActive })}>
                  {u.isActive ? "Deactivate" : "Reactivate"}
                </button>
              </td>
              <td>{formatDate(u.createdAt, false)}</td>
            </tr>
          ))}
        </tbody>
      </table>
    </>
  );
}

function CreateUser({ onCreated }: { onCreated: (u: User) => void }) {
  const empty = { displayName: "", email: "", role: "Teacher" as Role, password: "" };
  const [form, setForm] = useState(empty);
  const { busy, error, run } = useAction();

  const submit = async (e: FormEvent) => {
    e.preventDefault();
    if (await run(async () => onCreated(await usersApi.create(form)))) setForm(empty);
  };

  return (
    <details className="card card-body shadow-sm mb-3">
      <summary className="fw-semibold">Add a user</summary>
      <form className="row g-2 mt-2" onSubmit={submit}>
        <Alert error={error} />
        <div className="col-md-3"><input className="form-control" placeholder="Full name" aria-label="Full name" required value={form.displayName} onChange={(e) => setForm({ ...form, displayName: e.target.value })} /></div>
        <div className="col-md-3"><input type="email" className="form-control" placeholder="Email" aria-label="Email" required value={form.email} onChange={(e) => setForm({ ...form, email: e.target.value })} /></div>
        <div className="col-md-2">
          <select className="form-select" aria-label="Role" value={form.role} onChange={(e) => setForm({ ...form, role: e.target.value as Role })}>
            {roles.map((r) => <option key={r}>{r}</option>)}
          </select>
        </div>
        <div className="col-md-2"><input type="password" className="form-control" placeholder="Temporary password" aria-label="Temporary password" required minLength={8} value={form.password} onChange={(e) => setForm({ ...form, password: e.target.value })} /></div>
        <div className="col-md-2"><button className="btn btn-primary w-100" disabled={busy}>Add user</button></div>
      </form>
    </details>
  );
}
