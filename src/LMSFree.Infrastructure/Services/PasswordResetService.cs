using System.Security.Cryptography;
using System.Text;
using LMSFree.Domain;
using LMSFree.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace LMSFree.Infrastructure.Services;

public sealed class PasswordResetService(LmsDbContext db, TimeProvider clock)
{
    public static readonly TimeSpan Lifetime = TimeSpan.FromHours(1);

    /// <summary>Issues a single-use token and returns the raw value to email. Older tokens for the user stop working.</summary>
    public async Task<string> IssueAsync(Guid userId, CancellationToken ct = default)
    {
        db.PasswordResetTokens.RemoveRange(
            await db.PasswordResetTokens.Where(t => t.UserId == userId && t.UsedAt == null).ToListAsync(ct));

        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();
        db.PasswordResetTokens.Add(new PasswordResetToken
        {
            TokenHash = Hash(token),
            UserId = userId,
            ExpiresAt = clock.GetUtcNow().UtcDateTime.Add(Lifetime)
        });
        await db.SaveChangesAsync(ct);
        return token;
    }

    /// <summary>Marks the token used and returns its owner, or null when it's unknown, used, or expired.</summary>
    public async Task<Guid?> RedeemAsync(string token, CancellationToken ct = default)
    {
        var hash = Hash(token.Trim().ToLowerInvariant());
        var row = await db.PasswordResetTokens.FirstOrDefaultAsync(t => t.TokenHash == hash, ct);
        var now = clock.GetUtcNow().UtcDateTime;
        if (row is null || row.UsedAt is not null || row.ExpiresAt < now)
            return null;

        row.UsedAt = now;
        await db.SaveChangesAsync(ct);
        return row.UserId;
    }

    private static string Hash(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
