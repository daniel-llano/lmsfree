namespace LMSFree.Domain;

public enum UserRole
{
    Student,
    Teacher,
    Admin
}

public class User
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Email { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    public UserRole Role { get; set; } = UserRole.Student;

    // Deactivated accounts can't sign in, but their work stays on record.
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class Course
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Code { get; set; } = "";
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public Guid TeacherId { get; set; }
    public User Teacher { get; set; } = null!;
    public bool IsArchived { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public List<Enrollment> Enrollments { get; set; } = [];
    public List<Assignment> Assignments { get; set; } = [];
}

public class Enrollment
{
    public Guid CourseId { get; set; }
    public Course Course { get; set; } = null!;
    public Guid StudentId { get; set; }
    public User Student { get; set; } = null!;
    public DateTime EnrolledAt { get; set; } = DateTime.UtcNow;
}

public class Assignment
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CourseId { get; set; }
    public Course Course { get; set; } = null!;
    public string Title { get; set; } = "";
    public string Instructions { get; set; } = "";
    public DateTime? DueAt { get; set; }
    public int MaxPoints { get; set; } = 100;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public List<Submission> Submissions { get; set; } = [];
}

public class Submission
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid AssignmentId { get; set; }
    public Assignment Assignment { get; set; } = null!;
    public Guid StudentId { get; set; }
    public User Student { get; set; } = null!;
    public string? Text { get; set; }
    public string? FileKey { get; set; }
    public string? FileName { get; set; }
    public string? FileContentType { get; set; }
    public DateTime SubmittedAt { get; set; } = DateTime.UtcNow;
    public decimal? Score { get; set; }
    public string? Feedback { get; set; }
    public DateTime? GradedAt { get; set; }
}

public class PasswordResetToken
{
    // SHA-256 of the token that was emailed. The raw token is never stored.
    public string TokenHash { get; set; } = "";
    public Guid UserId { get; set; }
    public DateTime ExpiresAt { get; set; }
    public DateTime? UsedAt { get; set; }
}
