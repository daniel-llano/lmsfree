import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter } from "react-router";
import { describe, expect, it, vi } from "vitest";
import App from "@/App";
import { writeSession } from "@/api/client";
import { AuthProvider } from "@/auth/AuthContext";

function json(body: unknown, status = 200) {
  return new Response(JSON.stringify(body), { status, headers: { "Content-Type": "application/json" } });
}

function renderAt(path: string) {
  return render(
    <MemoryRouter initialEntries={[path]}>
      <AuthProvider>
        <App />
      </AuthProvider>
    </MemoryRouter>,
  );
}

const student = { id: "s1", email: "alice@school.edu", displayName: "Alice", role: "Student", isActive: true, createdAt: "2026-01-01" };
const teacher = { ...student, id: "t1", displayName: "Grace", role: "Teacher" };

function signedInAs(user: typeof student, routes: Record<string, unknown>) {
  writeSession({ token: "t", expiresAt: new Date(Date.now() + 60_000).toISOString() });
  return vi.spyOn(globalThis, "fetch").mockImplementation(async (input, init) => {
    const url = String(input);
    if (url.endsWith("/api/auth/me")) return json(user);
    const match = Object.keys(routes).find((r) => url.includes(r) && (!r.startsWith("PUT ") || init?.method === "PUT"));
    return match ? json(routes[match]) : json({ title: "Not found" }, 404);
  });
}

describe("routing", () => {
  it("sends guests to sign in", async () => {
    renderAt("/");
    expect(await screen.findByRole("heading", { name: "Sign in" })).toBeInTheDocument();
  });
});

describe("CoursesPage", () => {
  it("tells a student with no courses how to get enrolled", async () => {
    signedInAs(student, { "/api/courses": [] });
    renderAt("/");
    expect(await screen.findByText(/You're not enrolled in any course yet/)).toBeInTheDocument();
    expect(screen.queryByRole("link", { name: "New course" })).not.toBeInTheDocument();
  });

  it("lets teachers create a course", async () => {
    signedInAs(teacher, { "/api/courses": [{ id: "c1", code: "CS101", title: "Intro", teacherName: "Grace", studentCount: 2, assignmentCount: 1, isArchived: false }] });
    renderAt("/");
    expect(await screen.findByRole("heading", { name: "Intro" })).toBeInTheDocument();
    expect(screen.getByText(/2 students/)).toBeInTheDocument();
    expect(screen.getByRole("link", { name: "New course" })).toBeInTheDocument();
  });
});

describe("AssignmentPage", () => {
  const assignment = { id: "a1", courseId: "c1", courseTitle: "Intro", title: "FizzBuzz", instructions: "Print it.", dueAt: null, maxPoints: 20, canManage: false, mySubmission: null };

  it("lets a student submit text", async () => {
    const fetch = signedInAs(student, {
      "/api/assignments/a1/submission": { id: "x", studentId: "s1", studentName: "Alice", text: "done", fileName: null, submittedAt: "2026-01-02T10:00:00Z", isLate: false, score: null, feedback: null, gradedAt: null },
      "/api/assignments/a1": assignment,
    });
    renderAt("/assignments/a1");

    await userEvent.type(await screen.findByLabelText("Answer"), "done");
    await userEvent.click(screen.getByRole("button", { name: "Submit" }));

    await waitFor(() => expect(screen.getByRole("button", { name: "Resubmit" })).toBeInTheDocument());
    const put = fetch.mock.calls.find(([, init]) => init?.method === "PUT")!;
    expect(put[1]!.body).toBeInstanceOf(FormData);
  });

  it("shows the grade once it's graded", async () => {
    signedInAs(student, {
      "/api/assignments/a1": { ...assignment, mySubmission: { id: "x", studentId: "s1", studentName: "Alice", text: "done", fileName: null, submittedAt: "2026-01-02T10:00:00Z", isLate: false, score: 18, feedback: "Great", gradedAt: "2026-01-03" } },
    });
    renderAt("/assignments/a1");
    expect(await screen.findByText("Grade: 18 / 20")).toBeInTheDocument();
    expect(screen.queryByRole("button", { name: /submit/i })).not.toBeInTheDocument();
  });
});
