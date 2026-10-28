using LMSFree.Domain;
using LMSFree.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace LMSFree.Api;

public static class DataSeeder
{
    public static async Task SeedAsync(IServiceProvider services, IConfiguration config, ILogger logger, CancellationToken ct = default)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LmsDbContext>();

        if (db.Database.IsRelational() && config.GetValue("Database:MigrateOnStartup", false))
            await db.Database.MigrateAsync(ct);
        else if (!db.Database.IsRelational())
            await db.Database.EnsureCreatedAsync(ct);

        var adminEmail = config["Bootstrap:AdminEmail"]?.Trim().ToLowerInvariant();
        var adminPassword = config["Bootstrap:AdminPassword"];
        if (!string.IsNullOrEmpty(adminEmail) && !string.IsNullOrEmpty(adminPassword)
            && !await db.Users.AnyAsync(u => u.Email == adminEmail, ct))
        {
            db.Users.Add(new User { Email = adminEmail, DisplayName = "Administrator", Role = UserRole.Admin, PasswordHash = Hash(adminPassword) });
            await db.SaveChangesAsync(ct);
            logger.LogInformation("Created bootstrap admin {Email}", adminEmail);
        }

        if (config.GetValue<bool>("SeedSampleData") && !await db.Courses.AnyAsync(ct))
            await SeedSampleCourseAsync(db, ct);
    }

    private static async Task SeedSampleCourseAsync(LmsDbContext db, CancellationToken ct)
    {
        var teacher = new User { Email = "teacher@lmsfree.local", DisplayName = "Grace Hopper", Role = UserRole.Teacher, PasswordHash = Hash("Teacher@1234") };
        var alice = new User { Email = "alice@lmsfree.local", DisplayName = "Alice Martin", PasswordHash = Hash("Student@1234") };
        var bob = new User { Email = "bob@lmsfree.local", DisplayName = "Bob Chen", PasswordHash = Hash("Student@1234") };
        db.Users.AddRange(teacher, alice, bob);

        var course = new Course
        {
            Code = "CS101",
            Title = "Introduction to Programming",
            Description = "Variables, control flow, functions, and your first small programs.",
            Teacher = teacher,
            Enrollments = [new Enrollment { Student = alice }, new Enrollment { Student = bob }],
            Assignments =
            [
                new Assignment
                {
                    Title = "Hello, world",
                    Instructions = "Write a program that prints your name and today's date. Upload the source file.",
                    DueAt = DateTime.UtcNow.Date.AddDays(7),
                    MaxPoints = 10
                },
                new Assignment
                {
                    Title = "FizzBuzz",
                    Instructions = "Print 1 to 100. Multiples of 3 print Fizz, multiples of 5 print Buzz, both print FizzBuzz.",
                    DueAt = DateTime.UtcNow.Date.AddDays(14),
                    MaxPoints = 20
                }
            ]
        };
        db.Courses.Add(course);
        await db.SaveChangesAsync(ct);
    }

    private static string Hash(string password) => BCrypt.Net.BCrypt.HashPassword(password);
}
