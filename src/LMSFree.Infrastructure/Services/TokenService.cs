using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using LMSFree.Domain;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace LMSFree.Infrastructure.Services;

public sealed class JwtOptions
{
    public const string Section = "Jwt";

    public string Secret { get; set; } = "";
    public string Issuer { get; set; } = "LMSFree";
    public string Audience { get; set; } = "LMSFree";
    public int AccessTokenMinutes { get; set; } = 480;

    public SymmetricSecurityKey SigningKey => new(Encoding.UTF8.GetBytes(Secret));
}

public sealed class TokenService(IOptions<JwtOptions> options, TimeProvider clock)
{
    private readonly JwtOptions _options = options.Value;

    public (string Token, DateTime ExpiresAt) Issue(User user)
    {
        var expires = clock.GetUtcNow().UtcDateTime.AddMinutes(_options.AccessTokenMinutes);
        var jwt = new JwtSecurityToken(
            _options.Issuer,
            _options.Audience,
            [
                new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
                new Claim(JwtRegisteredClaimNames.Email, user.Email),
                new Claim(JwtRegisteredClaimNames.Name, user.DisplayName),
                new Claim(ClaimTypes.Role, user.Role.ToString()),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
            ],
            expires: expires,
            signingCredentials: new SigningCredentials(_options.SigningKey, SecurityAlgorithms.HmacSha256));
        return (new JwtSecurityTokenHandler().WriteToken(jwt), expires);
    }
}
