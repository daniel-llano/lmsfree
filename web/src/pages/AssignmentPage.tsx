import { useState, type FormEvent } from "react";
import { Link, useNavigate, useParams } from "react-router";
import { assignmentsApi, downloadSubmission } from "@/api/endpoints";
import type { AssignmentDetails, Submission } from "@/api/types";
import { Alert, formatDate, formatDue } from "@/components/Layout";
import { describe, useAction, useLoad } from "@/components/useAsync";

export function AssignmentPage() {
  const id = useParams().id!;
  const navigate = useNavigate();
  const { data: assignment, setData, error, status } = useLoad(() => assignmentsApi.get(id), [id]);
  const removal = useAction();

  if (status === 404) return <div className="alert alert-warning">This assignment doesn't exist or you're not in its course.</div>;
  if (!assignment) return error ? <Alert error={error} /> : <p className="text-muted">Loading…</p>;

  const overdue = assignment.dueAt !== null && new Date(assignment.dueAt) < new Date();

  return (
    <>
      <Link to={`/courses/${assignment.courseId}`} className="small">← {assignment.courseTitle}</Link>
      <div className="d-flex flex-wrap justify-content-between align-items-start gap-2 mt-1 mb-3">
        <div>
          <h1 className="h3 mb-0">{assignment.title}</h1>
          <span className={overdue ? "text-danger" : "text-muted"}>{formatDue(assignment.dueAt)}</span>
          <span className="text-muted"> · {assignment.maxPoints} points</span>
        </div>
        {assignment.canManage && (
          <button
            className="btn btn-sm btn-outline-danger"
            disabled={removal.busy}
            onClick={() =>
              confirm("Delete this assignment and every submission for it?") &&
              removal.run(async () => {
                await assignmentsApi.remove(id);
                navigate(`/courses/${assignment.courseId}`, { replace: true });
              })
            }
          >
            Delete assignment
          </button>
        )}
      </div>
      <Alert error={removal.error} />
      {assignment.instructions && <p className="instructions">{assignment.instructions}</p>}

      {assignment.canManage ? (
        <Gradebook assignment={assignment} />
      ) : (
        <MySubmission assignment={assignment} onChange={(s) => setData({ ...assignment, mySubmission: s })} />
      )}
    </>
  );
}

function MySubmission({ assignment, onChange }: { assignment: AssignmentDetails; onChange: (s: Submission) => void }) {
  const mine = assignment.mySubmission;
  const [text, setText] = useState(mine?.text ?? "");
  const [file, setFile] = useState<File | null>(null);
  const { busy, error, setError, run } = useAction();
  const graded = mine?.score != null;

  const submit = async (e: FormEvent) => {
    e.preventDefault();
    if (!text.trim() && !file) {
      setError("Add some text or attach a file.");
      return;
    }
    if (await run(async () => onChange(await assignmentsApi.submit(assignment.id, text, file)))) setFile(null);
  };

  return (
    <section className="card shadow-sm">
      <div className="card-body">
        <h2 className="h5">Your work</h2>
        {mine && (
          <p className="small text-muted">
            Submitted {formatDate(mine.submittedAt)}
            {mine.isLate && <span className="badge text-bg-warning ms-2">Late</span>}
            {mine.fileName && (
              <>
                {" · "}
                <button className="btn btn-link btn-sm p-0 align-baseline" onClick={() => downloadSubmission(mine).catch((e) => setError(describe(e)))}>
                  {mine.fileName}
                </button>
              </>
            )}
          </p>
        )}
        {graded ? (
          <div className="alert alert-success mb-0">
            <strong>Grade: {mine!.score} / {assignment.maxPoints}</strong>
            {mine!.feedback && <p className="mb-0 mt-1">{mine!.feedback}</p>}
          </div>
        ) : (
          <form onSubmit={submit}>
            <Alert error={error} />
            <div className="mb-3">
              <label className="form-label" htmlFor="answer">Answer</label>
              <textarea id="answer" className="form-control" rows={5} maxLength={20000} value={text} onChange={(e) => setText(e.target.value)} />
            </div>
            <div className="mb-3">
              <label className="form-label" htmlFor="file">Attachment (optional, up to 10 MB)</label>
              <input id="file" type="file" className="form-control" onChange={(e) => setFile(e.target.files?.[0] ?? null)} />
            </div>
            <button className="btn btn-primary" disabled={busy}>{mine ? "Resubmit" : "Submit"}</button>
          </form>
        )}
      </div>
    </section>
  );
}

function Gradebook({ assignment }: { assignment: AssignmentDetails }) {
  const { data: submissions, setData, error } = useLoad(() => assignmentsApi.submissions(assignment.id), [assignment.id]);

  return (
    <section>
      <h2 className="h5">Submissions</h2>
      <Alert error={error} />
      {submissions?.length === 0 && <p className="text-muted">No submissions yet.</p>}
      <div className="d-grid gap-3">
        {submissions?.map((s) => (
          <GradeCard
            key={s.id}
            submission={s}
            maxPoints={assignment.maxPoints}
            onGraded={(updated) => setData(submissions.map((x) => (x.id === updated.id ? updated : x)))}
          />
        ))}
      </div>
    </section>
  );
}

function GradeCard({ submission, maxPoints, onGraded }: { submission: Submission; maxPoints: number; onGraded: (s: Submission) => void }) {
  const [score, setScore] = useState(submission.score?.toString() ?? "");
  const [feedback, setFeedback] = useState(submission.feedback ?? "");
  const { busy, error, setError, run } = useAction();

  const save = async (e: FormEvent) => {
    e.preventDefault();
    await run(async () => onGraded(await assignmentsApi.grade(submission.id, Number(score), feedback)));
  };

  return (
    <div className="card shadow-sm">
      <div className="card-body">
        <div className="d-flex justify-content-between flex-wrap gap-2">
          <strong>{submission.studentName}</strong>
          <span className="small text-muted">
            {formatDate(submission.submittedAt)}
            {submission.isLate && <span className="badge text-bg-warning ms-2">Late</span>}
            {submission.score != null && <span className="badge text-bg-success ms-2">Graded</span>}
          </span>
        </div>
        {submission.text && <p className="instructions mt-2 mb-2">{submission.text}</p>}
        {submission.fileName && (
          <button className="btn btn-link btn-sm p-0" onClick={() => downloadSubmission(submission).catch((e) => setError(describe(e)))}>
            Download {submission.fileName}
          </button>
        )}
        <form className="row g-2 align-items-end mt-1" onSubmit={save}>
          <div className="col-sm-3">
            <label className="form-label small" htmlFor={`score-${submission.id}`}>Score (of {maxPoints})</label>
            <input id={`score-${submission.id}`} type="number" step="0.5" min={0} max={maxPoints} className="form-control form-control-sm" required value={score} onChange={(e) => setScore(e.target.value)} />
          </div>
          <div className="col-sm-7">
            <label className="form-label small" htmlFor={`feedback-${submission.id}`}>Feedback</label>
            <input id={`feedback-${submission.id}`} className="form-control form-control-sm" maxLength={4000} value={feedback} onChange={(e) => setFeedback(e.target.value)} />
          </div>
          <div className="col-sm-2">
            <button className="btn btn-sm btn-primary w-100" disabled={busy}>Save grade</button>
          </div>
        </form>
        <Alert error={error} />
      </div>
    </div>
  );
}
