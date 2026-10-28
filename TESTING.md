# Testing LMSFree

## Unit and integration

```bash
./scripts/test.sh
```

```powershell
.\scripts\test.ps1
```

- `tests/LMSFree.Api.Tests` runs the API through `WebApplicationFactory` on EF in-memory with a temp upload folder and a captured email sender. It covers registration, deactivated logins, the forgot/reset flow (including token replay), role checks, per-course access, enrollment rules, file upload and download, grading bounds, and the student grades view.
- `web/src/test` covers the fetch client, route guards, and the student submit and grade views with Vitest and Testing Library.

CI also runs `dotnet ef migrations has-pending-model-changes`.

## End to end

Needs Docker and the Playwright CLI (`npm install -g playwright`).

```bash
./scripts/e2e.sh
```

It starts `infra/docker-compose.yml` (PostgreSQL, API, web, Mailpit) and runs `e2e/ui.spec.ts` against http://localhost:5174:

- a teacher creates an assignment, a student submits a file, the teacher downloads and grades it, and the student sees the grade
- an admin adds a teacher, who creates a course and enrolls a newly registered student
- password reset by reading the email from Mailpit's API
