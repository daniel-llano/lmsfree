using System.Collections.Concurrent;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using LMSFree.Api.Endpoints;
using LMSFree.Infrastructure.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace LMSFree.Api.Tests;

public sealed class LmsFactory : WebApplicationFactory<Program>
{
    public const string AdminEmail = "admin@test.local";
    public const string Password = "Passw0rd!";

    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };

    public CapturingEmailSender Mail { get; } = new();

    private readonly string _storage = Path.Combine(Path.GetTempPath(), $"lmsfree-{Guid.NewGuid():N}");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("Database:InMemoryName", Guid.NewGuid().ToString());
        builder.UseSetting("Jwt:Secret", "test-only-signing-key-that-is-long-enough");
        builder.UseSetting("Bootstrap:AdminEmail", AdminEmail);
        builder.UseSetting("Bootstrap:AdminPassword", Password);
        builder.UseSetting("Storage:Path", _storage);
        builder.UseSetting("RateLimiting:AuthPermitsPerMinute", "10000");
        builder.ConfigureServices(s => s.AddSingleton<IEmailSender>(Mail));
    }

    public async Task<HttpClient> SignInAsync(string email, string password = Password)
    {
        var client = CreateClient();
        var resp = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, password));
        resp.EnsureSuccessStatusCode();
        var auth = await resp.Content.ReadFromJsonAsync<AuthResponse>(Json);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth!.Token);
        return client;
    }

    public Task<HttpClient> AdminAsync() => SignInAsync(AdminEmail);

    /// <summary>Creates an account with the given role through the admin API and signs in as it.</summary>
    public async Task<(HttpClient Client, UserResponse User)> UserAsync(string role)
    {
        var admin = await AdminAsync();
        var email = $"{role.ToLowerInvariant()}-{Guid.NewGuid():N}@test.local";
        var resp = await admin.PostAsJsonAsync("/api/users", new CreateUserRequest(email, $"Test {role}", role, Password));
        resp.EnsureSuccessStatusCode();
        var user = await resp.Content.ReadFromJsonAsync<UserResponse>(Json);
        return (await SignInAsync(email), user!);
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        try { Directory.Delete(_storage, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }
}

public sealed class CapturingEmailSender : IEmailSender
{
    public ConcurrentQueue<(string To, string Subject, string Body)> Sent { get; } = new();

    public Task SendAsync(string to, string subject, string body, CancellationToken ct = default)
    {
        Sent.Enqueue((to, subject, body));
        return Task.CompletedTask;
    }
}
