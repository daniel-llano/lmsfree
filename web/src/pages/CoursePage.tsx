import { useState, type FormEvent } from "react";
import { Link, useParams } from "react-router";
import { coursesApi } from "@/api/endpoints";
import type { CourseDetails, SaveAssignment } from "@/api/types";
import { Alert, formatDate, formatDue } from "@/components/Layout";
import { useAction, useLoad } from "@/components/useAsync";

export function CoursePage() {
  const id = useParams().id!;
  const { data: course, setData: setCourse, error, status } = useLoad(() => coursesApi.get(id), [id]);
  const [tab, setTab] = useState<"assignments" | "students" | "settings">("assignments");

  if (status === 404) return <div className="alert alert-warning">This course doesn't exist or you're not part of it.</div>;
  if (!course) return error ? <Alert error={error} /> : <p className="text-muted">Loading…</p>;

  return (
    <>
      <div className="mb-3">
        <span className="badge text-bg-secondary">{course.code}</span>
        {course.isArchived && <span className="badge text-bg-warning ms-1">Archived</span>}
        <h1 className="h3 mt-1 mb-0">{course.title}</h1>
        <p className="text-muted mb-1">Taught by {course.teacherName}</p>
        {course.description && <p className="mb-0">{course.description}</p>}
      </div>

      {course.canManage && (
        <ul className="nav nav-tabs mb-3">
          {(["assignments", "students", "settings"] as const).map((t) => (
            <li className="nav-item" key={t}>
              <button className={`nav-link ${tab === t ? "active" : ""}`} onClick={() => setTab(t)}>
                {t[0].toUpperCase() + t.slice(1)}
              </button>
            </li>
          ))}
        </ul>
      )}

      {tab === "assignments" && <Assignments courseId={id} canManage={course.canManage} />}
      {tab === "students" && course.canManage && <Students courseId={id} />}
      {tab === "settings" && course.canManage && <Settings course={course} onSaved={setCourse} />}
    </>
  );
}

function Assignments({ courseId, canManage }: { courseId: string; canManage: boolean }) {
  const { data: assignments, error, reload } = useLoad(() => coursesApi.assignments(courseId), [courseId]);
  const [creating, setCreating] = useState(false);

  return (
    <section>
      <div className="d-flex justify-content-between align-items-center mb-2">
        <h2 className="h5 mb-0">Assignments</h2>
        {canManage && !creating && (
          <button className="btn btn-sm btn-success" onClick={() => setCreating(true)}>New assignment</button>
        )}
      </div>
      {creating && (
        <AssignmentForm
          courseId={courseId}
          onDone={async () => {
            setCreating(false);
            await reload();
          }}
        />
      )}
      <Alert error={error} />
      {assignments?.length === 0 && <p className="text-muted">No assignments yet.</p>}
      {assignments && assignments.length > 0 && (
        <div className="list-group shadow-sm">
          {assignments.map((a) => (
            <Link key={a.id} to={`/assignments/${a.id}`} className="list-group-item list-group-item-action d-flex justify-content-between align-items-center">
              <div>
                <div className="fw-semibold">{a.title}</div>
                <div className="small text-muted">{formatDue(a.dueAt)} · {a.maxPoints} points</div>
              </div>
              {canManage ? (
                <span className="badge text-bg-light">{a.submissionCount} submitted</span>
              ) : a.mySubmission?.score != null ? (
                <span className="badge text-bg-success">{a.mySubmission.score} / {a.maxPoints}</span>
              ) : a.mySubmission ? (
                <span className="badge text-bg-info">Submitted</span>
              ) : (
                <span className="badge text-bg-warning">To do</span>
              )}
            </Link>
          ))}
        </div>
      )}
    </section>
  );
}

function AssignmentForm({ courseId, onDone }: { courseId: string; onDone: () => void }) {
  const [form, setForm] = useState<SaveAssignment & { due: string }>({ title: "", instructions: "", dueAt: null, maxPoints: 100, due: "" });
  const { busy, error, run } = useAction();

  const submit = async (e: FormEvent) => {
    e.preventDefault();
    const payload = { title: form.title, instructions: form.instructions, maxPoints: form.maxPoints, dueAt: form.due ? new Date(form.due).toISOString() : null };
    if (await run(async () => void (await coursesApi.createAssignment(courseId, payload)))) onDone();
  };

  return (
    <form className="card card-body shadow-sm mb-3" onSubmit={submit}>
      <Alert error={error} />
      <div className="mb-2">
        <label className="form-label" htmlFor="a-title">Title</label>
        <input id="a-title" className="form-control" required minLength={3} maxLength={200} value={form.title} onChange={(e) => setForm({ ...form, title: e.target.value })} />
      </div>
      <div className="mb-2">
        <label className="form-label" htmlFor="a-instructions">Instructions</label>
        <textarea id="a-instructions" className="form-control" rows={3} value={form.instructions} onChange={(e) => setForm({ ...form, instructions: e.target.value })} />
      </div>
      <div className="row">
        <div className="col-md-6 mb-2">
          <label className="form-label" htmlFor="a-due">Due</label>
          <input id="a-due" type="datetime-local" className="form-control" value={form.due} onChange={(e) => setForm({ ...form, due: e.target.value })} />
        </div>
        <div className="col-md-6 mb-2">
          <label className="form-label" htmlFor="a-points">Max points</label>
          <input id="a-points" type="number" min={1} max={1000} className="form-control" required value={form.maxPoints} onChange={(e) => setForm({ ...form, maxPoints: Number(e.target.value) })} />
        </div>
      </div>
      <div className="d-flex gap-2">
        <button className="btn btn-primary" disabled={busy}>Create assignment</button>
        <button type="button" className="btn btn-outline-secondary" onClick={onDone}>Cancel</button>
      </div>
    </form>
  );
}

function Students({ courseId }: { courseId: string }) {
  const { data: students, error, reload } = useLoad(() => coursesApi.students(courseId), [courseId]);
  const [email, setEmail] = useState("");
  const action = useAction();

  const enroll = async (e: FormEvent) => {
    e.preventDefault();
    if (await action.run(async () => void (await coursesApi.enroll(courseId, email)))) {
      setEmail("");
      await reload();
    }
  };

  return (
    <section>
      <form className="d-flex gap-2 mb-3" onSubmit={enroll}>
        <input type="email" className="form-control" placeholder="student@school.edu" aria-label="Student email" required value={email} onChange={(e) => setEmail(e.target.value)} />
        <button className="btn btn-primary text-nowrap" disabled={action.busy}>Enroll student</button>
      </form>
      <Alert error={action.error ?? error} />
      {students?.length === 0 && <p className="text-muted">Nobody is enrolled yet.</p>}
      {students && students.length > 0 && (
        <table className="table bg-white shadow-sm align-middle">
          <thead className="table-light">
            <tr><th>Name</th><th>Email</th><th>Enrolled</th><th></th></tr>
          </thead>
          <tbody>
            {students.map((s) => (
              <tr key={s.id}>
                <td>{s.displayName}</td>
                <td>{s.email}</td>
                <td>{formatDate(s.enrolledAt, false)}</td>
                <td className="text-end">
                  <button
                    className="btn btn-sm btn-outline-danger"
                    disabled={action.busy}
                    onClick={() => confirm(`Remove ${s.displayName} from this course?`) && action.run(async () => { await coursesApi.withdraw(courseId, s.id); await reload(); })}
                  >
                    Remove
                  </button>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      )}
    </section>
  );
}

function Settings({ course, onSaved }: { course: CourseDetails; onSaved: (c: CourseDetails) => void }) {
  const [form, setForm] = useState({ code: course.code, title: course.title, description: course.description, isArchived: course.isArchived });
  const [saved, setSaved] = useState(false);
  const { busy, error, run } = useAction();

  const submit = async (e: FormEvent) => {
    e.preventDefault();
    setSaved(false);
    if (await run(async () => onSaved(await coursesApi.update(course.id, form)))) setSaved(true);
  };

  return (
    <form className="card card-body shadow-sm" style={{ maxWidth: "40rem" }} onSubmit={submit}>
      {saved && <div className="alert alert-success">Saved.</div>}
      <Alert error={error} />
      <div className="row">
        <div className="col-md-4 mb-3">
          <label className="form-label" htmlFor="s-code">Code</label>
          <input id="s-code" className="form-control" required value={form.code} onChange={(e) => setForm({ ...form, code: e.target.value })} />
        </div>
        <div className="col-md-8 mb-3">
          <label className="form-label" htmlFor="s-title">Title</label>
          <input id="s-title" className="form-control" required value={form.title} onChange={(e) => setForm({ ...form, title: e.target.value })} />
        </div>
      </div>
      <div className="mb-3">
        <label className="form-label" htmlFor="s-description">Description</label>
        <textarea id="s-description" className="form-control" rows={3} value={form.description} onChange={(e) => setForm({ ...form, description: e.target.value })} />
      </div>
      <div className="form-check mb-3">
        <input id="s-archived" type="checkbox" className="form-check-input" checked={form.isArchived} onChange={(e) => setForm({ ...form, isArchived: e.target.checked })} />
        <label className="form-check-label" htmlFor="s-archived">Archived (hidden from course lists)</label>
      </div>
      <div><button className="btn btn-primary" disabled={busy}>Save course</button></div>
    </form>
  );
}
