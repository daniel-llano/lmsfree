import { useState, type FormEvent } from "react";
import { Link, useNavigate } from "react-router";
import { coursesApi, usersApi } from "@/api/endpoints";
import type { SaveCourse } from "@/api/types";
import { useAuth } from "@/auth/AuthContext";
import { Alert } from "@/components/Layout";
import { useAction, useLoad } from "@/components/useAsync";

export function CoursesPage() {
  const { user, isStaff } = useAuth();
  const [showArchived, setShowArchived] = useState(false);
  const { data: courses, error } = useLoad(() => coursesApi.list(showArchived), [showArchived]);

  return (
    <>
      <div className="page-title">
        <h1>{user?.role === "Student" ? "My courses" : "Courses"}</h1>
        {isStaff && (
          <div className="d-flex align-items-center gap-3">
            <div className="form-check mb-0">
              <input id="archived" type="checkbox" className="form-check-input" checked={showArchived} onChange={(e) => setShowArchived(e.target.checked)} />
              <label className="form-check-label small" htmlFor="archived">Show archived</label>
            </div>
            <Link to="/courses/new" className="btn btn-success">New course</Link>
          </div>
        )}
      </div>
      <Alert error={error} />
      {courses?.length === 0 && (
        <div className="alert alert-info">
          {user?.role === "Student" ? "You're not enrolled in any course yet. Your teacher adds you with your email." : "No courses yet."}
        </div>
      )}
      <div className="row g-3">
        {courses?.map((c) => (
          <div key={c.id} className="col-md-6 col-lg-4">
            <Link to={`/courses/${c.id}`} className="card h-100 text-decoration-none shadow-sm course-card">
              <div className="card-body">
                <div className="d-flex justify-content-between">
                  <span className="badge text-bg-secondary">{c.code}</span>
                  {c.isArchived && <span className="badge text-bg-warning">Archived</span>}
                </div>
                <h2 className="h5 mt-2 mb-1">{c.title}</h2>
                <p className="text-muted small mb-0">
                  {c.teacherName} · {c.assignmentCount} assignment{c.assignmentCount === 1 ? "" : "s"}
                  {user?.role !== "Student" && ` · ${c.studentCount} student${c.studentCount === 1 ? "" : "s"}`}
                </p>
              </div>
            </Link>
          </div>
        ))}
      </div>
    </>
  );
}

export function NewCoursePage() {
  const { isAdmin } = useAuth();
  const navigate = useNavigate();
  const [form, setForm] = useState<SaveCourse>({ code: "", title: "", description: "", teacherId: null });
  const { data: teachers } = useLoad(() => (isAdmin ? usersApi.list("Teacher") : Promise.resolve([])), [isAdmin]);
  const { busy, error, run } = useAction();

  const submit = async (e: FormEvent) => {
    e.preventDefault();
    await run(async () => {
      const course = await coursesApi.create({ ...form, teacherId: form.teacherId || null });
      navigate(`/courses/${course.id}`);
    });
  };

  return (
    <div className="card shadow-sm" style={{ maxWidth: "40rem" }}>
      <div className="card-body">
        <h1 className="h4 mb-3">New course</h1>
        <Alert error={error} />
        <form onSubmit={submit}>
          <div className="row">
            <div className="col-md-4 mb-3">
              <label className="form-label" htmlFor="code">Code</label>
              <input id="code" className="form-control text-uppercase" required pattern="[A-Za-z0-9\-]{2,20}" placeholder="CS101" value={form.code} onChange={(e) => setForm({ ...form, code: e.target.value })} />
            </div>
            <div className="col-md-8 mb-3">
              <label className="form-label" htmlFor="title">Title</label>
              <input id="title" className="form-control" required minLength={3} maxLength={200} value={form.title} onChange={(e) => setForm({ ...form, title: e.target.value })} />
            </div>
          </div>
          <div className="mb-3">
            <label className="form-label" htmlFor="description">Description</label>
            <textarea id="description" className="form-control" rows={3} maxLength={4000} value={form.description} onChange={(e) => setForm({ ...form, description: e.target.value })} />
          </div>
          {isAdmin && (
            <div className="mb-3">
              <label className="form-label" htmlFor="teacher">Teacher</label>
              <select id="teacher" className="form-select" value={form.teacherId ?? ""} onChange={(e) => setForm({ ...form, teacherId: e.target.value })}>
                <option value="">Me</option>
                {teachers?.filter((t) => t.isActive).map((t) => (
                  <option key={t.id} value={t.id}>{t.displayName}</option>
                ))}
              </select>
            </div>
          )}
          <div className="d-flex gap-2">
            <button className="btn btn-primary" disabled={busy}>Create course</button>
            <Link to="/" className="btn btn-outline-secondary">Cancel</Link>
          </div>
        </form>
      </div>
    </div>
  );
}
