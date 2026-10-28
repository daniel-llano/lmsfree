using LMSFree.Api.Security;
using LMSFree.Domain;
using LMSFree.Infrastructure.Data;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace LMSFree.Api.Endpoints;

public static class UserEndpoints
{
    public static void MapUsers(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/users").WithTags("Users").RequireAuthorization("Admin");
        g.MapGet("/", ListAsync);
        g.MapPost("/", CreateAsync);
        g.MapPut("/{id:guid}", UpdateAsync);
    }

    private static async Task<Ok<List<UserResponse>>> ListAsync(LmsDbContext db, string? role, string? q, CancellationToken ct)
    {
        var users = db.Users.AsNoTracking();
        if (Enum.TryParse<UserRole>(role, true, out var r))
            users = users.Where(u => u.Role == r);
        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim().ToLower();
            users = users.Where(u => u.Email.Contains(term) || u.DisplayName.ToLower().Contains(term));
        }
        var list = await users.OrderBy(u => u.DisplayName).Take(500).ToListAsync(ct);
        return TypedResults.Ok(list.Select(u => u.ToResponse()).ToList());
    }

    private static async Task<Results<Created<UserResponse>, ProblemHttpResult, ValidationProblem>> CreateAsync(
        CreateUserRequest req, LmsDbContext db, CancellationToken ct)
    {
        if (Passwords.Check(req.Password) is { } problem) return problem;

        var email = AuthEndpoints.Normalize(req.Email);
        if (await db.Users.AnyAsync(u => u.Email == email, ct))
            return TypedResults.Problem($"{email} already has an account.", statusCode: 409, title: "Email already registered");

        var user = new User
        {
            Email = email,
            DisplayName = req.DisplayName.Trim(),
            Role = Enum.Parse<UserRole>(req.Role),
            PasswordHash = Passwords.Hash(req.Password)
        };
        db.Users.Add(user);
        await db.SaveChangesAsync(ct);
        return TypedResults.Created($"/api/users/{user.Id}", user.ToResponse());
    }

    private static async Task<Results<Ok<UserResponse>, NotFound, ProblemHttpResult>> UpdateAsync(
        Guid id, UpdateUserRequest req, HttpContext http, LmsDbContext db, CancellationToken ct)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == id, ct);
        if (user is null) return TypedResults.NotFound();

        var role = Enum.Parse<UserRole>(req.Role);
        if (id == http.User.Id() && (role != user.Role || !req.IsActive))
            return TypedResults.Problem("Ask another admin to change your own role or status.", statusCode: 422, title: "Can't change your own access");

        if (user.Role == UserRole.Teacher && role != UserRole.Teacher
            && await db.Courses.AnyAsync(c => c.TeacherId == id && !c.IsArchived, ct))
            return TypedResults.Problem("Reassign or archive their courses first.", statusCode: 422, title: "This teacher still has active courses");

        user.DisplayName = req.DisplayName.Trim();
        user.Role = role;
        user.IsActive = req.IsActive;
        await db.SaveChangesAsync(ct);
        return TypedResults.Ok(user.ToResponse());
    }
}
