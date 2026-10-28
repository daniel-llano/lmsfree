using LMSFree.Domain;
using Microsoft.EntityFrameworkCore;

namespace LMSFree.Infrastructure.Data;

public class LmsDbContext(DbContextOptions<LmsDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Course> Courses => Set<Course>();
    public DbSet<Enrollment> Enrollments => Set<Enrollment>();
    public DbSet<Assignment> Assignments => Set<Assignment>();
    public DbSet<Submission> Submissions => Set<Submission>();
    public DbSet<PasswordResetToken> PasswordResetTokens => Set<PasswordResetToken>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<User>(e =>
        {
            e.Property(u => u.Email).HasMaxLength(256).IsRequired();
            e.HasIndex(u => u.Email).IsUnique();
            e.Property(u => u.DisplayName).HasMaxLength(100).IsRequired();
            e.Property(u => u.PasswordHash).HasMaxLength(100).IsRequired();
            e.Property(u => u.Role).HasConversion<string>().HasMaxLength(20);
        });

        b.Entity<Course>(e =>
        {
            e.Property(c => c.Code).HasMaxLength(20).IsRequired();
            e.HasIndex(c => c.Code).IsUnique();
            e.Property(c => c.Title).HasMaxLength(200).IsRequired();
            e.Property(c => c.Description).HasMaxLength(4000);
            e.HasOne(c => c.Teacher).WithMany().HasForeignKey(c => c.TeacherId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<Enrollment>(e =>
        {
            e.HasKey(x => new { x.CourseId, x.StudentId });
            e.HasOne(x => x.Course).WithMany(c => c.Enrollments).HasForeignKey(x => x.CourseId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Student).WithMany().HasForeignKey(x => x.StudentId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => x.StudentId);
        });

        b.Entity<Assignment>(e =>
        {
            e.Property(a => a.Title).HasMaxLength(200).IsRequired();
            e.Property(a => a.Instructions).HasMaxLength(8000);
            e.HasOne(a => a.Course).WithMany(c => c.Assignments).HasForeignKey(a => a.CourseId).OnDelete(DeleteBehavior.Cascade);
            e.ToTable(t => t.HasCheckConstraint("CK_Assignments_MaxPoints", "\"MaxPoints\" BETWEEN 1 AND 1000"));
        });

        b.Entity<Submission>(e =>
        {
            e.HasIndex(s => new { s.AssignmentId, s.StudentId }).IsUnique();
            e.Property(s => s.Text).HasMaxLength(20000);
            e.Property(s => s.FileKey).HasMaxLength(100);
            e.Property(s => s.FileName).HasMaxLength(255);
            e.Property(s => s.FileContentType).HasMaxLength(100);
            e.Property(s => s.Score).HasPrecision(7, 2);
            e.Property(s => s.Feedback).HasMaxLength(4000);
            e.HasOne(s => s.Assignment).WithMany(a => a.Submissions).HasForeignKey(s => s.AssignmentId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(s => s.Student).WithMany().HasForeignKey(s => s.StudentId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<PasswordResetToken>(e =>
        {
            e.HasKey(t => t.TokenHash);
            e.Property(t => t.TokenHash).HasMaxLength(64);
            e.HasIndex(t => t.UserId);
            e.HasOne<User>().WithMany().HasForeignKey(t => t.UserId).OnDelete(DeleteBehavior.Cascade);
        });
    }
}
