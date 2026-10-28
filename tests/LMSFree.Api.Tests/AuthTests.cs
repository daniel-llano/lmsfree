using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using FluentAssertions;
using LMSFree.Api.Endpoints;

namespace LMSFree.Api.Tests;

public class AuthTests(LmsFactory factory) : IClassFixture<LmsFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Register_CreatesStudent()
    {
        var resp = await _client.PostAsJsonAsync("/api/auth/register", new RegisterRequest("New.Student@Test.local", "Learn1234", "New Student"));

        resp.StatusCode.Should().Be(HttpStatusCode.Created);
        var auth = await resp.Content.ReadFromJsonAsync<AuthResponse>(LmsFactory.Json);
        auth!.User.Role.Should().Be("Student");
        auth.User.Email.Should().Be("new.student@test.local");
    }

    [Fact]
    public async Task Register_WeakPassword_Returns400()
    {
        var resp = await _client.PostAsJsonAsync("/api/auth/register", new RegisterRequest("weak@test.local", "password", "Weak"));

        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Login_DeactivatedAccount_Returns401()
    {
        var (_, user) = await factory.UserAsync("Student");
        var admin = await factory.AdminAsync();
        await admin.PutAsJsonAsync($"/api/users/{user.Id}", new UpdateUserRequest(user.DisplayName, "Student", false));

        var resp = await _client.PostAsJsonAsync("/api/auth/login", new LoginRequest(user.Email, LmsFactory.Password));

        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ForgotPassword_UnknownEmail_StillReturns202AndSendsNothing()
    {
        var before = factory.Mail.Sent.Count;

        var resp = await _client.PostAsJsonAsync("/api/auth/forgot-password", new ForgotPasswordRequest("nobody@test.local"));

        resp.StatusCode.Should().Be(HttpStatusCode.Accepted);
        factory.Mail.Sent.Count.Should().Be(before);
    }

    [Fact]
    public async Task ResetPassword_WithEmailedToken_WorksOnce()
    {
        var (_, user) = await factory.UserAsync("Student");

        var forgot = await _client.PostAsJsonAsync("/api/auth/forgot-password", new ForgotPasswordRequest(user.Email));
        forgot.StatusCode.Should().Be(HttpStatusCode.Accepted);
        (await forgot.Content.ReadAsStringAsync()).Should().NotContain("token");

        var mail = factory.Mail.Sent.Last(m => m.To == user.Email);
        var token = Regex.Match(mail.Body, "token=([0-9a-f]{64})").Groups[1].Value;

        var reset = await _client.PostAsJsonAsync("/api/auth/reset-password", new ResetPasswordRequest(token, "NewPass123"));
        reset.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var replay = await _client.PostAsJsonAsync("/api/auth/reset-password", new ResetPasswordRequest(token, "Another123"));
        replay.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        (await _client.PostAsJsonAsync("/api/auth/login", new LoginRequest(user.Email, "NewPass123")))
            .StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ChangePassword_RequiresCurrentPassword()
    {
        var (client, _) = await factory.UserAsync("Student");

        (await client.PutAsJsonAsync("/api/auth/me/password", new ChangePasswordRequest("wrong", "Changed123")))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await client.PutAsJsonAsync("/api/auth/me/password", new ChangePasswordRequest(LmsFactory.Password, "Changed123")))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Users_AreAdminOnly()
    {
        var (teacher, _) = await factory.UserAsync("Teacher");

        (await teacher.GetAsync("/api/users")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Admin_CannotDemoteThemselves()
    {
        var admin = await factory.AdminAsync();
        var me = await admin.GetFromJsonAsync<UserResponse>("/api/auth/me", LmsFactory.Json);

        var resp = await admin.PutAsJsonAsync($"/api/users/{me!.Id}", new UpdateUserRequest("Me", "Student", true));

        resp.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Theory]
    [InlineData("/health/live")]
    [InlineData("/health/ready")]
    public async Task Health_Returns200(string path)
    {
        (await _client.GetAsync(path)).StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
