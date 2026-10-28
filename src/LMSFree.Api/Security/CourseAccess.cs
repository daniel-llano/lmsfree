using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using LMSFree.Domain;
using LMSFree.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace LMSFree.Api.Security;

public static class ClaimsPrincipalExtensions
{
    public static Guid Id(this ClaimsPrincipal user) =>
        Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier) ?? user.FindFirstValue(JwtRegisteredClaimNames.Sub)
            ?? throw new InvalidOperationException("Token has no subject."));

    public static bool IsAdmin(this ClaimsPrincipal user) => user.IsInRole(nameof(UserRole.Admin));
}

public enum CourseRelation
{
    None,
    Student,
    Teacher,
    Admin
}

/// <summary>Who the caller is to a course. Teachers and admins manage it, enrolled students can read it.</summary>
public sealed class CourseAccess(LmsDbContext db, IHttpContextAccessor http)
{
    private ClaimsPrincipal User => http.HttpContext!.User;

    public async Task<CourseRelation> RelationAsync(Guid courseId, CancellationToken ct)
    {
        if (User.IsAdmin())
            return await db.Courses.AnyAsync(c => c.Id == courseId, ct) ? CourseRelation.Admin : CourseRelation.None;

        var userId = User.Id();
        var course = await db.Courses.AsNoTracking()
            .Where(c => c.Id == courseId)
            .Select(c => new { c.TeacherId, Enrolled = c.Enrollments.Any(e => e.StudentId == userId) })
            .FirstOrDefaultAsync(ct);

        if (course is null) return CourseRelation.None;
        if (course.TeacherId == userId) return CourseRelation.Teacher;
        return course.Enrolled ? CourseRelation.Student : CourseRelation.None;
    }

    public static bool CanManage(CourseRelation r) => r is CourseRelation.Teacher or CourseRelation.Admin;
}
