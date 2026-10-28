using LMSFree.Api.Security;
using LMSFree.Domain;
using LMSFree.Infrastructure.Data;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace LMSFree.Api.Endpoints;

public static class CourseEndpoints
{
    public static void MapCourses(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/courses").WithTags("Courses").RequireAuthorization();
        g.MapGet("/", ListAsync);
        g.MapGet("/{id:guid}", GetAsync).WithName("GetCourse");
        g.MapPost("/", CreateAsync).RequireAuthorization("Staff");
        g.MapPut("/{id:guid}", UpdateAsync);
        g.MapGet("/{id:guid}/students", StudentsAsync);
        g.MapPost("/{id:guid}/students", EnrollAsync);
        g.MapDelete("/{id:guid}/students/{studentId:guid}", WithdrawAsync);
    }

    /// <summary>Admins see every course, teachers see theirs, students see the ones they're enrolled in.</summary>
    private static async Task<Ok<List<CourseSummary>>> ListAsync(HttpContext http, LmsDbContext db, bool includeArchived = false, CancellationToken ct = default)
    {
        var user = http.User;
        var userId = user.Id();
        var courses = db.Courses.AsNoTracking();
        if (!user.IsAdmin())
            courses = courses.Where(c => c.TeacherId == userId || c.Enrollments.Any(e => e.StudentId == userId));
        if (!includeArchived)
            courses = courses.Where(c => !c.IsArchived);

        var list = await courses
            .OrderBy(c => c.Code)
            .Select(c => new CourseSummary(c.Id, c.Code, c.Title, c.Teacher.DisplayName, c.Enrollments.Count, c.Assignments.Count, c.IsArchived))
            .ToListAsync(ct);
        return TypedResults.Ok(list);
    }

    private static async Task<Results<Ok<CourseDetails>, NotFound>> GetAsync(Guid id, LmsDbContext db, CourseAccess access, CancellationToken ct)
    {
        var relation = await access.RelationAsync(id, ct);
        if (relation == CourseRelation.None) return TypedResults.NotFound();

        var c = await db.Courses.AsNoTracking().Include(c => c.Teacher).FirstAsync(c => c.Id == id, ct);
        return TypedResults.Ok(new CourseDetails(c.Id, c.Code, c.Title, c.Description, c.TeacherId, c.Teacher.DisplayName,
            c.IsArchived, CourseAccess.CanManage(relation)));
    }

    private static async Task<Results<CreatedAtRoute<CourseDetails>, ProblemHttpResult>> CreateAsync(
        SaveCourseRequest req, HttpContext http, LmsDbContext db, CancellationToken ct)
    {
        var teacherId = http.User.IsAdmin() ? req.TeacherId ?? http.User.Id() : http.User.Id();
        if (await TeacherProblemAsync(db, teacherId, ct) is { } problem) return problem;

        var code = req.Code.Trim().ToUpperInvariant();
        if (await db.Courses.AnyAsync(c => c.Code == code, ct))
            return TypedResults.Problem($"Course code {code} is taken.", statusCode: 409, title: "Duplicate course code");

        var course = new Course { Code = code, Title = req.Title.Trim(), Description = req.Description?.Trim() ?? "", TeacherId = teacherId };
        db.Courses.Add(course);
        await db.SaveChangesAsync(ct);

        var teacher = await db.Users.AsNoTracking().FirstAsync(u => u.Id == teacherId, ct);
        return TypedResults.CreatedAtRoute(
            new CourseDetails(course.Id, course.Code, course.Title, course.Description, teacherId, teacher.DisplayName, false, true),
            "GetCourse", new { id = course.Id });
    }

    private static async Task<Results<Ok<CourseDetails>, NotFound, ForbidHttpResult, ProblemHttpResult>> UpdateAsync(
        Guid id, SaveCourseRequest req, HttpContext http, LmsDbContext db, CourseAccess access, CancellationToken ct)
    {
        var relation = await access.RelationAsync(id, ct);
        if (relation == CourseRelation.None) return TypedResults.NotFound();
        if (!CourseAccess.CanManage(relation)) return TypedResults.Forbid();

        var course = await db.Courses.FirstAsync(c => c.Id == id, ct);
        var code = req.Code.Trim().ToUpperInvariant();
        if (code != course.Code && await db.Courses.AnyAsync(c => c.Code == code, ct))
            return TypedResults.Problem($"Course code {code} is taken.", statusCode: 409, title: "Duplicate course code");

        // Only admins move a course to another teacher.
        if (http.User.IsAdmin() && req.TeacherId is { } newTeacher && newTeacher != course.TeacherId)
        {
            if (await TeacherProblemAsync(db, newTeacher, ct) is { } problem) return problem;
            course.TeacherId = newTeacher;
        }

        course.Code = code;
        course.Title = req.Title.Trim();
        course.Description = req.Description?.Trim() ?? "";
        course.IsArchived = req.IsArchived;
        await db.SaveChangesAsync(ct);

        var teacher = await db.Users.AsNoTracking().FirstAsync(u => u.Id == course.TeacherId, ct);
        return TypedResults.Ok(new CourseDetails(course.Id, course.Code, course.Title, course.Description, course.TeacherId,
            teacher.DisplayName, course.IsArchived, true));
    }

    private static async Task<Results<Ok<List<StudentResponse>>, NotFound, ForbidHttpResult>> StudentsAsync(
        Guid id, LmsDbContext db, CourseAccess access, CancellationToken ct)
    {
        var relation = await access.RelationAsync(id, ct);
        if (relation == CourseRelation.None) return TypedResults.NotFound();
        if (!CourseAccess.CanManage(relation)) return TypedResults.Forbid();

        var students = await db.Enrollments.AsNoTracking()
            .Where(e => e.CourseId == id)
            .OrderBy(e => e.Student.DisplayName)
            .Select(e => new StudentResponse(e.StudentId, e.Student.Email, e.Student.DisplayName, e.EnrolledAt))
            .ToListAsync(ct);
        return TypedResults.Ok(students);
    }

    private static async Task<Results<Created<StudentResponse>, NotFound, ForbidHttpResult, ProblemHttpResult>> EnrollAsync(
        Guid id, EnrollRequest req, LmsDbContext db, CourseAccess access, CancellationToken ct)
    {
        var relation = await access.RelationAsync(id, ct);
        if (relation == CourseRelation.None) return TypedResults.NotFound();
        if (!CourseAccess.CanManage(relation)) return TypedResults.Forbid();

        var email = AuthEndpoints.Normalize(req.Email);
        var student = await db.Users.FirstOrDefaultAsync(u => u.Email == email, ct);
        if (student is null)
            return TypedResults.Problem($"No account uses {email}. Ask them to register first.", statusCode: 404, title: "Student not found");
        if (student.Role != UserRole.Student || !student.IsActive)
            return TypedResults.Problem($"{email} isn't an active student account.", statusCode: 422, title: "Can't enroll this account");
        if (await db.Enrollments.AnyAsync(e => e.CourseId == id && e.StudentId == student.Id, ct))
            return TypedResults.Problem($"{student.DisplayName} is already in this course.", statusCode: 409, title: "Already enrolled");

        var enrollment = new Enrollment { CourseId = id, StudentId = student.Id };
        db.Enrollments.Add(enrollment);
        await db.SaveChangesAsync(ct);
        return TypedResults.Created($"/api/courses/{id}/students",
            new StudentResponse(student.Id, student.Email, student.DisplayName, enrollment.EnrolledAt));
    }

    private static async Task<Results<NoContent, NotFound, ForbidHttpResult>> WithdrawAsync(
        Guid id, Guid studentId, LmsDbContext db, CourseAccess access, CancellationToken ct)
    {
        var relation = await access.RelationAsync(id, ct);
        if (relation == CourseRelation.None) return TypedResults.NotFound();
        if (!CourseAccess.CanManage(relation)) return TypedResults.Forbid();

        var enrollment = await db.Enrollments.FirstOrDefaultAsync(e => e.CourseId == id && e.StudentId == studentId, ct);
        if (enrollment is null) return TypedResults.NotFound();
        db.Enrollments.Remove(enrollment);
        await db.SaveChangesAsync(ct);
        return TypedResults.NoContent();
    }

    private static async Task<ProblemHttpResult?> TeacherProblemAsync(LmsDbContext db, Guid teacherId, CancellationToken ct)
    {
        var ok = await db.Users.AnyAsync(u => u.Id == teacherId && u.IsActive && (u.Role == UserRole.Teacher || u.Role == UserRole.Admin), ct);
        return ok ? null : TypedResults.Problem("Pick an active teacher or admin account.", statusCode: 422, title: "Invalid teacher");
    }
}
