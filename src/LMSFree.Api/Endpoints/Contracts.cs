using System.ComponentModel.DataAnnotations;

namespace LMSFree.Api.Endpoints;

public record RegisterRequest(
    [property: Required, EmailAddress, MaxLength(256)] string Email,
    [property: Required, StringLength(128, MinimumLength = 8)] string Password,
    [property: Required, StringLength(100, MinimumLength = 2)] string DisplayName);

public record LoginRequest([property: Required] string Email, [property: Required] string Password);

public record ForgotPasswordRequest([property: Required, EmailAddress] string Email);

public record ResetPasswordRequest(
    [property: Required, MaxLength(128)] string Token,
    [property: Required, StringLength(128, MinimumLength = 8)] string NewPassword);

public record ChangePasswordRequest(
    [property: Required] string CurrentPassword,
    [property: Required, StringLength(128, MinimumLength = 8)] string NewPassword);

public record UserResponse(Guid Id, string Email, string DisplayName, string Role, bool IsActive, DateTime CreatedAt);

public record AuthResponse(string Token, DateTime ExpiresAt, UserResponse User);

public record CreateUserRequest(
    [property: Required, EmailAddress, MaxLength(256)] string Email,
    [property: Required, StringLength(100, MinimumLength = 2)] string DisplayName,
    [property: Required, AllowedValues("Student", "Teacher", "Admin")] string Role,
    [property: Required, StringLength(128, MinimumLength = 8)] string Password);

public record UpdateUserRequest(
    [property: Required, StringLength(100, MinimumLength = 2)] string DisplayName,
    [property: Required, AllowedValues("Student", "Teacher", "Admin")] string Role,
    bool IsActive);

public record CourseSummary(Guid Id, string Code, string Title, string TeacherName, int StudentCount, int AssignmentCount, bool IsArchived);

public record CourseDetails(Guid Id, string Code, string Title, string Description, Guid TeacherId, string TeacherName, bool IsArchived, bool CanManage);

public record SaveCourseRequest(
    [property: Required, RegularExpression("^[A-Za-z0-9-]{2,20}$", ErrorMessage = "Use 2-20 letters, digits, or dashes.")] string Code,
    [property: Required, StringLength(200, MinimumLength = 3)] string Title,
    [property: StringLength(4000)] string? Description,
    Guid? TeacherId,
    bool IsArchived = false);

public record EnrollRequest([property: Required, EmailAddress] string Email);

public record StudentResponse(Guid Id, string Email, string DisplayName, DateTime EnrolledAt);

public record SaveAssignmentRequest(
    [property: Required, StringLength(200, MinimumLength = 3)] string Title,
    [property: StringLength(8000)] string? Instructions,
    DateTime? DueAt,
    [property: Range(1, 1000)] int MaxPoints = 100);

public record SubmissionResponse(
    Guid Id,
    Guid StudentId,
    string StudentName,
    string? Text,
    string? FileName,
    DateTime SubmittedAt,
    bool IsLate,
    decimal? Score,
    string? Feedback,
    DateTime? GradedAt);

public record AssignmentSummary(Guid Id, string Title, DateTime? DueAt, int MaxPoints, int SubmissionCount, SubmissionResponse? MySubmission);

public record AssignmentDetails(
    Guid Id,
    Guid CourseId,
    string CourseTitle,
    string Title,
    string Instructions,
    DateTime? DueAt,
    int MaxPoints,
    bool CanManage,
    SubmissionResponse? MySubmission);

public record GradeRequest([property: Range(0, 1000)] decimal Score, [property: StringLength(4000)] string? Feedback);

public record GradeRow(Guid CourseId, string CourseTitle, Guid AssignmentId, string AssignmentTitle, int MaxPoints, DateTime? SubmittedAt, decimal? Score, string? Feedback);
