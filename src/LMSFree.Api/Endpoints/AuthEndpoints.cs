using LMSFree.Api.Security;
using LMSFree.Domain;
using LMSFree.Infrastructure.Data;
using LMSFree.Infrastructure.Services;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace LMSFree.Api.Endpoints;

public static class AuthEndpoints
{
    public static void MapAuth(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/auth").WithTags("Auth");
        g.MapPost("/register", RegisterAsync).RequireRateLimiting("auth");
        g.MapPost("/login", LoginAsync).RequireRateLimiting("auth");
        g.MapPost("/forgot-password", ForgotAsync).RequireRateLimiting("auth");
        g.MapPost("/reset-password", ResetAsync).RequireRateLimiting("auth");
        g.MapGet("/me", MeAsync).RequireAuthorization();
        g.MapPut("/me/password", ChangePasswordAsync).RequireAuthorization();
    }

    private static async Task<Results<Created<AuthResponse>, ProblemHttpResult, ValidationProblem>> RegisterAsync(
        RegisterRequest req, LmsDbContext db, TokenService tokens, CancellationToken ct)
    {
        if (Passwords.Check(req.Password) is { } problem) return problem;

        var email = Normalize(req.Email);
        if (await db.Users.AnyAsync(u => u.Email == email, ct))
            return TypedResults.Problem("Sign in instead, or reset your password.", statusCode: 409, title: "Email already registered");

        var user = new User { Email = email, DisplayName = req.DisplayName.Trim(), PasswordHash = Passwords.Hash(req.Password) };
        db.Users.Add(user);
        await db.SaveChangesAsync(ct);
        return TypedResults.Created("/api/auth/me", Respond(user, tokens));
    }

    private static async Task<Results<Ok<AuthResponse>, UnauthorizedHttpResult>> LoginAsync(
        LoginRequest req, LmsDbContext db, TokenService tokens, CancellationToken ct)
    {
        var email = Normalize(req.Email);
        var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Email == email, ct);
        if (user is null || !user.IsActive || !BCrypt.Net.BCrypt.Verify(req.Password, user.PasswordHash))
            return TypedResults.Unauthorized();
        return TypedResults.Ok(Respond(user, tokens));
    }

    // Always answers the same way so nobody can probe which emails have accounts.
    private static async Task<Accepted> ForgotAsync(
        ForgotPasswordRequest req, LmsDbContext db, PasswordResetService resets, IEmailSender email,
        IConfiguration config, CancellationToken ct)
    {
        var address = Normalize(req.Email);
        var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Email == address && u.IsActive, ct);
        if (user is not null)
        {
            var token = await resets.IssueAsync(user.Id, ct);
            var link = $"{config["App:PublicUrl"]?.TrimEnd('/')}/reset-password?token={token}";
            await email.SendAsync(user.Email, "Reset your LMSFree password",
                $"Hi {user.DisplayName},\n\nUse this link within an hour to choose a new password:\n{link}\n\n" +
                "If you didn't ask for this, you can ignore the message.", ct);
        }
        return TypedResults.Accepted((string?)null);
    }

    private static async Task<Results<NoContent, ProblemHttpResult, ValidationProblem>> ResetAsync(
        ResetPasswordRequest req, LmsDbContext db, PasswordResetService resets, CancellationToken ct)
    {
        if (Passwords.Check(req.NewPassword, "newPassword") is { } problem) return problem;

        var userId = await resets.RedeemAsync(req.Token, ct);
        var user = userId is null ? null : await db.Users.FirstOrDefaultAsync(u => u.Id == userId, ct);
        if (user is null)
            return TypedResults.Problem("Request a new reset link.", statusCode: 400, title: "This reset link is invalid or has expired");

        user.PasswordHash = Passwords.Hash(req.NewPassword);
        await db.SaveChangesAsync(ct);
        return TypedResults.NoContent();
    }

    private static async Task<Results<Ok<UserResponse>, UnauthorizedHttpResult>> MeAsync(HttpContext http, LmsDbContext db, CancellationToken ct)
    {
        var id = http.User.Id();
        var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == id && u.IsActive, ct);
        return user is null ? TypedResults.Unauthorized() : TypedResults.Ok(user.ToResponse());
    }

    private static async Task<Results<NoContent, ProblemHttpResult, ValidationProblem>> ChangePasswordAsync(
        ChangePasswordRequest req, HttpContext http, LmsDbContext db, CancellationToken ct)
    {
        if (Passwords.Check(req.NewPassword, "newPassword") is { } problem) return problem;

        var id = http.User.Id();
        var user = await db.Users.FirstAsync(u => u.Id == id, ct);
        if (!BCrypt.Net.BCrypt.Verify(req.CurrentPassword, user.PasswordHash))
            return TypedResults.Problem("Your current password is wrong.", statusCode: 400, title: "Password not changed");

        user.PasswordHash = Passwords.Hash(req.NewPassword);
        await db.SaveChangesAsync(ct);
        return TypedResults.NoContent();
    }

    internal static string Normalize(string email) => email.Trim().ToLowerInvariant();

    private static AuthResponse Respond(User user, TokenService tokens)
    {
        var (token, expires) = tokens.Issue(user);
        return new AuthResponse(token, expires, user.ToResponse());
    }
}

internal static class Passwords
{
    public static string Hash(string password) => BCrypt.Net.BCrypt.HashPassword(password);

    public static ValidationProblem? Check(string password, string field = "password") =>
        password.Any(char.IsLetter) && password.Any(char.IsDigit)
            ? null
            : TypedResults.ValidationProblem(new Dictionary<string, string[]> { [field] = ["Use at least one letter and one digit."] });
}

internal static class UserMapping
{
    public static UserResponse ToResponse(this User u) => new(u.Id, u.Email, u.DisplayName, u.Role.ToString(), u.IsActive, u.CreatedAt);
}
