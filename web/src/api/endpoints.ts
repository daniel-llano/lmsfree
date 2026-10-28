import { ApiError, readSession, request } from "./client";
import type {
  AssignmentDetails,
  AssignmentSummary,
  AuthResponse,
  CourseDetails,
  CourseSummary,
  GradeRow,
  Role,
  SaveAssignment,
  SaveCourse,
  Student,
  Submission,
  User,
} from "./types";

const json = (method: string, body: unknown): RequestInit => ({ method, body: JSON.stringify(body) });

export const authApi = {
  login: (email: string, password: string) => request<AuthResponse>("/api/auth/login", json("POST", { email, password })),
  register: (email: string, password: string, displayName: string) =>
    request<AuthResponse>("/api/auth/register", json("POST", { email, password, displayName })),
  forgot: (email: string) => request<void>("/api/auth/forgot-password", json("POST", { email })),
  reset: (token: string, newPassword: string) => request<void>("/api/auth/reset-password", json("POST", { token, newPassword })),
  me: () => request<User>("/api/auth/me"),
  changePassword: (currentPassword: string, newPassword: string) =>
    request<void>("/api/auth/me/password", json("PUT", { currentPassword, newPassword })),
};

export const usersApi = {
  list: (role?: Role) => request<User[]>(`/api/users${role ? `?role=${role}` : ""}`),
  create: (user: { email: string; displayName: string; role: Role; password: string }) => request<User>("/api/users", json("POST", user)),
  update: (id: string, user: { displayName: string; role: Role; isActive: boolean }) => request<User>(`/api/users/${id}`, json("PUT", user)),
};

export const coursesApi = {
  list: (includeArchived = false) => request<CourseSummary[]>(`/api/courses${includeArchived ? "?includeArchived=true" : ""}`),
  get: (id: string) => request<CourseDetails>(`/api/courses/${id}`),
  create: (course: SaveCourse) => request<CourseDetails>("/api/courses", json("POST", course)),
  update: (id: string, course: SaveCourse) => request<CourseDetails>(`/api/courses/${id}`, json("PUT", course)),
  students: (id: string) => request<Student[]>(`/api/courses/${id}/students`),
  enroll: (id: string, email: string) => request<Student>(`/api/courses/${id}/students`, json("POST", { email })),
  withdraw: (id: string, studentId: string) => request<void>(`/api/courses/${id}/students/${studentId}`, { method: "DELETE" }),
  assignments: (id: string) => request<AssignmentSummary[]>(`/api/courses/${id}/assignments`),
  createAssignment: (id: string, a: SaveAssignment) => request<AssignmentDetails>(`/api/courses/${id}/assignments`, json("POST", a)),
};

export const assignmentsApi = {
  get: (id: string) => request<AssignmentDetails>(`/api/assignments/${id}`),
  update: (id: string, a: SaveAssignment) => request<AssignmentDetails>(`/api/assignments/${id}`, json("PUT", a)),
  remove: (id: string) => request<void>(`/api/assignments/${id}`, { method: "DELETE" }),
  submissions: (id: string) => request<Submission[]>(`/api/assignments/${id}/submissions`),
  submit: (id: string, text: string, file: File | null) => {
    const form = new FormData();
    if (text.trim()) form.append("text", text);
    if (file) form.append("file", file);
    return request<Submission>(`/api/assignments/${id}/submission`, { method: "PUT", body: form });
  },
  grade: (submissionId: string, score: number, feedback: string) =>
    request<Submission>(`/api/submissions/${submissionId}/grade`, json("PUT", { score, feedback })),
  myGrades: () => request<GradeRow[]>("/api/me/grades"),
};

/** Fetches a submitted file with the user's token and hands it to the browser as a download. */
export async function downloadSubmission(submission: Submission) {
  const session = readSession();
  const base = (import.meta.env.VITE_API_URL ?? "").replace(/\/$/, "");
  const res = await fetch(`${base}/api/submissions/${submission.id}/file`, {
    headers: session ? { Authorization: `Bearer ${session.token}` } : {},
  });
  if (!res.ok) throw new ApiError(res.status, "Couldn't download the file.");
  const url = URL.createObjectURL(await res.blob());
  const link = document.createElement("a");
  link.href = url;
  link.download = submission.fileName ?? "submission";
  link.click();
  URL.revokeObjectURL(url);
}
