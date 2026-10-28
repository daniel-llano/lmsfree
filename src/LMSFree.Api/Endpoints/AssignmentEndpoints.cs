using LMSFree.Api.Security;
using LMSFree.Domain;
using LMSFree.Infrastructure.Data;
using LMSFree.Infrastructure.Services;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LMSFree.Api.Endpoints;

public static class AssignmentEndpoints
{
    public const long MaxUploadBytes = 10 * 1024 * 1024;

    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".pdf", ".doc", ".docx", ".odt", ".txt", ".md", ".rtf", ".zip", ".png", ".jpg", ".jpeg",
        ".py", ".cs", ".js", ".ts", ".java", ".c", ".cpp", ".html", ".css", ".ipynb"
    };

    public static void MapAssignments(this IEndpointRouteBuilder app)
    {
        var courses = app.MapGroup("/api/courses/{courseId:guid}/assignments").WithTags("Assignments").RequireAuthorization();
        courses.MapGet("/", ListAsync);
        courses.MapPost("/", CreateAsync);

        var g = app.MapGroup("/api/assignments/{id:guid}").WithTags("Assignments").RequireAuthorization();
        g.MapGet("/", GetAsync);
        g.MapPut("/", UpdateAsync);
        g.MapDelete("/", DeleteAsync);
        g.MapGet("/submissions", SubmissionsAsync);
        g.MapPut("/submission", SubmitAsync).DisableAntiforgery();

        var s = app.MapGroup("/api/submissions/{id:guid}").WithTags("Submissions").RequireAuthorization();
        s.MapGet("/file", DownloadAsync);
        s.MapPut("/grade", GradeAsync);

        app.MapGet("/api/me/grades", MyGradesAsync).WithTags("Submissions").RequireAuthorization();
    }

    private static async Task<Results<Ok<List<AssignmentSummary>>, NotFound>> ListAsync(
        Guid courseId, HttpContext http, LmsDbContext db, CourseAccess access, CancellationToken ct)
    {
        var relation = await access.RelationAsync(courseId, ct);
        if (relation == CourseRelation.None) return TypedResults.NotFound();

        var userId = http.User.Id();
        var rows = await db.Assignments.AsNoTracking()
            .Where(a => a.CourseId == courseId)
            .OrderBy(a => a.DueAt == null).ThenBy(a => a.DueAt).ThenBy(a => a.CreatedAt)
            .Select(a => new
            {
                a.Id, a.Title, a.DueAt, a.MaxPoints,
                Count = a.Submissions.Count,
                Mine = a.Submissions.Where(s => s.StudentId == userId).Select(s => new { s, s.Student.DisplayName }).FirstOrDefault()
            })
            .ToListAsync(ct);

        return TypedResults.Ok(rows.Select(r => new AssignmentSummary(r.Id, r.Title, r.DueAt, r.MaxPoints,
            CourseAccess.CanManage(relation) ? r.Count : 0,
            r.Mine is null ? null : ToResponse(r.Mine.s, r.Mine.DisplayName, r.DueAt))).ToList());
    }

    private static async Task<Results<Created<AssignmentDetails>, NotFound, ForbidHttpResult>> CreateAsync(
        Guid courseId, SaveAssignmentRequest req, LmsDbContext db, CourseAccess access, CancellationToken ct)
    {
        var relation = await access.RelationAsync(courseId, ct);
        if (relation == CourseRelation.None) return TypedResults.NotFound();
        if (!CourseAccess.CanManage(relation)) return TypedResults.Forbid();

        var course = await db.Courses.AsNoTracking().FirstAsync(c => c.Id == courseId, ct);
        var assignment = new Assignment
        {
            CourseId = courseId,
            Title = req.Title.Trim(),
            Instructions = req.Instructions?.Trim() ?? "",
            DueAt = req.DueAt?.ToUniversalTime(),
            MaxPoints = req.MaxPoints
        };
        db.Assignments.Add(assignment);
        await db.SaveChangesAsync(ct);
        return TypedResults.Created($"/api/assignments/{assignment.Id}",
            new AssignmentDetails(assignment.Id, courseId, course.Title, assignment.Title, assignment.Instructions,
                assignment.DueAt, assignment.MaxPoints, true, null));
    }

    private static async Task<Results<Ok<AssignmentDetails>, NotFound>> GetAsync(
        Guid id, HttpContext http, LmsDbContext db, CourseAccess access, CancellationToken ct)
    {
        var a = await db.Assignments.AsNoTracking().Include(a => a.Course).FirstOrDefaultAsync(a => a.Id == id, ct);
        if (a is null) return TypedResults.NotFound();
        var relation = await access.RelationAsync(a.CourseId, ct);
        if (relation == CourseRelation.None) return TypedResults.NotFound();

        var userId = http.User.Id();
        var mine = await db.Submissions.AsNoTracking().Include(s => s.Student)
            .FirstOrDefaultAsync(s => s.AssignmentId == id && s.StudentId == userId, ct);

        return TypedResults.Ok(new AssignmentDetails(a.Id, a.CourseId, a.Course.Title, a.Title, a.Instructions, a.DueAt, a.MaxPoints,
            CourseAccess.CanManage(relation), mine is null ? null : ToResponse(mine, mine.Student.DisplayName, a.DueAt)));
    }

    private static async Task<Results<Ok<AssignmentDetails>, NotFound, ForbidHttpResult, ProblemHttpResult>> UpdateAsync(
        Guid id, SaveAssignmentRequest req, LmsDbContext db, CourseAccess access, CancellationToken ct)
    {
        var a = await db.Assignments.Include(a => a.Course).FirstOrDefaultAsync(a => a.Id == id, ct);
        if (a is null) return TypedResults.NotFound();
        var relation = await access.RelationAsync(a.CourseId, ct);
        if (relation == CourseRelation.None) return TypedResults.NotFound();
        if (!CourseAccess.CanManage(relation)) return TypedResults.Forbid();

        var highestScore = await db.Submissions.Where(s => s.AssignmentId == id && s.Score != null).MaxAsync(s => s.Score, ct);
        if (highestScore > req.MaxPoints)
            return TypedResults.Problem($"A submission already scored {highestScore}.", statusCode: 422, title: "Max points is lower than an existing grade");

        a.Title = req.Title.Trim();
        a.Instructions = req.Instructions?.Trim() ?? "";
        a.DueAt = req.DueAt?.ToUniversalTime();
        a.MaxPoints = req.MaxPoints;
        await db.SaveChangesAsync(ct);
        return TypedResults.Ok(new AssignmentDetails(a.Id, a.CourseId, a.Course.Title, a.Title, a.Instructions, a.DueAt, a.MaxPoints, true, null));
    }

    private static async Task<Results<NoContent, NotFound, ForbidHttpResult>> DeleteAsync(
        Guid id, LmsDbContext db, CourseAccess access, IFileStore files, CancellationToken ct)
    {
        var a = await db.Assignments.Include(a => a.Submissions).FirstOrDefaultAsync(a => a.Id == id, ct);
        if (a is null) return TypedResults.NotFound();
        var relation = await access.RelationAsync(a.CourseId, ct);
        if (relation == CourseRelation.None) return TypedResults.NotFound();
        if (!CourseAccess.CanManage(relation)) return TypedResults.Forbid();

        var keys = a.Submissions.Select(s => s.FileKey).OfType<string>().ToList();
        db.Assignments.Remove(a);
        await db.SaveChangesAsync(ct);
        keys.ForEach(files.Delete);
        return TypedResults.NoContent();
    }

    private static async Task<Results<Ok<List<SubmissionResponse>>, NotFound, ForbidHttpResult>> SubmissionsAsync(
        Guid id, LmsDbContext db, CourseAccess access, CancellationToken ct)
    {
        var a = await db.Assignments.AsNoTracking().FirstOrDefaultAsync(a => a.Id == id, ct);
        if (a is null) return TypedResults.NotFound();
        var relation = await access.RelationAsync(a.CourseId, ct);
        if (relation == CourseRelation.None) return TypedResults.NotFound();
        if (!CourseAccess.CanManage(relation)) return TypedResults.Forbid();

        var list = await db.Submissions.AsNoTracking().Include(s => s.Student)
            .Where(s => s.AssignmentId == id)
            .OrderBy(s => s.Student.DisplayName)
            .ToListAsync(ct);
        return TypedResults.Ok(list.Select(s => ToResponse(s, s.Student.DisplayName, a.DueAt)).ToList());
    }

    /// <summary>A student hands in text, a file, or both. They can resubmit until it's graded.</summary>
    private static async Task<Results<Ok<SubmissionResponse>, NotFound, ForbidHttpResult, ProblemHttpResult, ValidationProblem>> SubmitAsync(
        Guid id, [FromForm] string? text, IFormFile? file, HttpContext http, LmsDbContext db, CourseAccess access,
        IFileStore files, CancellationToken ct)
    {
        var a = await db.Assignments.AsNoTracking().FirstOrDefaultAsync(a => a.Id == id, ct);
        if (a is null) return TypedResults.NotFound();
        var relation = await access.RelationAsync(a.CourseId, ct);
        if (relation == CourseRelation.None) return TypedResults.NotFound();
        if (relation != CourseRelation.Student) return TypedResults.Forbid();

        text = string.IsNullOrWhiteSpace(text) ? null : text.Trim();
        if (text is null && file is null)
            return TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["submission"] = ["Add some text or attach a file."] });
        if (text?.Length > 20_000)
            return TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["text"] = ["Keep the text under 20,000 characters."] });

        var extension = file is null ? null : Path.GetExtension(file.FileName);
        if (file is not null && (file.Length == 0 || file.Length > MaxUploadBytes || !AllowedExtensions.Contains(extension ?? "")))
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                ["file"] = [$"Attach a document, image, archive, or source file up to {MaxUploadBytes / 1024 / 1024} MB."]
            });

        var userId = http.User.Id();
        var submission = await db.Submissions.Include(s => s.Student)
            .FirstOrDefaultAsync(s => s.AssignmentId == id && s.StudentId == userId, ct);
        if (submission?.Score is not null)
            return TypedResults.Problem("Ask your teacher if you need to resubmit.", statusCode: 422, title: "This submission is already graded");

        if (submission is null)
        {
            submission = new Submission { AssignmentId = id, StudentId = userId };
            db.Submissions.Add(submission);
        }

        var oldKey = submission.FileKey;
        if (file is not null)
        {
            await using var stream = file.OpenReadStream();
            submission.FileKey = await files.SaveAsync(stream, extension!, ct);
            submission.FileName = Path.GetFileName(file.FileName);
            submission.FileContentType = file.ContentType;
        }
        submission.Text = text;
        submission.SubmittedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        if (file is not null && oldKey is not null) files.Delete(oldKey);

        var name = submission.Student?.DisplayName ?? (await db.Users.FindAsync([userId], ct))!.DisplayName;
        return TypedResults.Ok(ToResponse(submission, name, a.DueAt));
    }

    private static async Task<Results<FileStreamHttpResult, NotFound>> DownloadAsync(
        Guid id, HttpContext http, LmsDbContext db, CourseAccess access, IFileStore files, CancellationToken ct)
    {
        var s = await db.Submissions.AsNoTracking().Include(s => s.Assignment).FirstOrDefaultAsync(s => s.Id == id, ct);
        if (s?.FileKey is null) return TypedResults.NotFound();

        var relation = await access.RelationAsync(s.Assignment.CourseId, ct);
        var allowed = CourseAccess.CanManage(relation) || (relation == CourseRelation.Student && s.StudentId == http.User.Id());
        if (!allowed) return TypedResults.NotFound();

        var stream = files.OpenRead(s.FileKey);
        if (stream is null) return TypedResults.NotFound();

        // Served as a download with a generic type so an uploaded file can never run in the browser.
        return TypedResults.File(stream, "application/octet-stream", s.FileName ?? "submission");
    }

    private static async Task<Results<Ok<SubmissionResponse>, NotFound, ForbidHttpResult, ValidationProblem>> GradeAsync(
        Guid id, GradeRequest req, LmsDbContext db, CourseAccess access, CancellationToken ct)
    {
        var s = await db.Submissions.Include(s => s.Assignment).Include(s => s.Student).FirstOrDefaultAsync(s => s.Id == id, ct);
        if (s is null) return TypedResults.NotFound();
        var relation = await access.RelationAsync(s.Assignment.CourseId, ct);
        if (relation == CourseRelation.None) return TypedResults.NotFound();
        if (!CourseAccess.CanManage(relation)) return TypedResults.Forbid();

        if (req.Score > s.Assignment.MaxPoints)
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                ["score"] = [$"The score can't be more than {s.Assignment.MaxPoints}."]
            });

        s.Score = req.Score;
        s.Feedback = string.IsNullOrWhiteSpace(req.Feedback) ? null : req.Feedback.Trim();
        s.GradedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return TypedResults.Ok(ToResponse(s, s.Student.DisplayName, s.Assignment.DueAt));
    }

    private static async Task<Ok<List<GradeRow>>> MyGradesAsync(HttpContext http, LmsDbContext db, CancellationToken ct)
    {
        var userId = http.User.Id();
        var rows = await db.Assignments.AsNoTracking()
            .Where(a => a.Course.Enrollments.Any(e => e.StudentId == userId))
            .OrderBy(a => a.Course.Code).ThenBy(a => a.DueAt)
            .Select(a => new
            {
                a.CourseId, CourseTitle = a.Course.Title, a.Id, a.Title, a.MaxPoints,
                Mine = a.Submissions.Where(s => s.StudentId == userId).Select(s => new { s.SubmittedAt, s.Score, s.Feedback }).FirstOrDefault()
            })
            .ToListAsync(ct);

        return TypedResults.Ok(rows.Select(r => new GradeRow(r.CourseId, r.CourseTitle, r.Id, r.Title, r.MaxPoints,
            r.Mine?.SubmittedAt, r.Mine?.Score, r.Mine?.Feedback)).ToList());
    }

    private static SubmissionResponse ToResponse(Submission s, string studentName, DateTime? dueAt) =>
        new(s.Id, s.StudentId, studentName, s.Text, s.FileName, s.SubmittedAt, dueAt is { } due && s.SubmittedAt > due,
            s.Score, s.Feedback, s.GradedAt);
}
