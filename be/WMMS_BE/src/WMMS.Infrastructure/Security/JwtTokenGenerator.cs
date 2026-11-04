using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using WMMS.Application.Interfaces.Services;
using WMMS.Domain.Entities.Identity;

namespace WMMS.Infrastructure.Security;

/// <summary>
/// Triển khai Bộ khởi tạo Token JWT và mã băm SHA-256 (Report 4 Class Diagram 3.2.1 <c>JwtTokenGenerator</c>).
/// Tương thích với cấu hình <c>JwtSettings</c> trong <c>appsettings.json</c> (SecretKey, Issuer, Audience, ExpirationMinutes)
/// và đóng gói đầy đủ danh sách vai trò (<c>ClaimTypes.Role</c>) phục vụ Multi-Role RBAC (GBR-04).
/// </summary>
public class JwtTokenGenerator : IJwtTokenGenerator
{
    private readonly IConfiguration _configuration;

    public JwtTokenGenerator(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    /// <inheritdoc />
    public (string AccessToken, DateTime ExpiresAt) GenerateAccessToken(User user, IList<string> roles)
    {
        var jwtSettings = _configuration.GetSection("JwtSettings");
        var secretKey = jwtSettings["SecretKey"] ?? "WMMS_SECRET_KEY_WOODEN_MANUFACTURING_MANAGEMENT_SYSTEM_2026_SECURITY_KEY";
        var issuer = jwtSettings["Issuer"] ?? "WMMS.WebAPI";
        var audience = jwtSettings["Audience"] ?? "WMMS.Client";
        var expirationMinutes = int.TryParse(jwtSettings["ExpirationMinutes"], out var mins) ? mins : 120;

        var expiresAt = DateTime.UtcNow.AddMinutes(expirationMinutes);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.UserId.ToString()),
            new(ClaimTypes.NameIdentifier, user.UserId.ToString()),
            new(JwtRegisteredClaimNames.UniqueName, user.Username),
            new(ClaimTypes.Name, user.Username),
            new(JwtRegisteredClaimNames.Email, user.Email),
            new(ClaimTypes.Email, user.Email),
            new("fullName", user.FullName),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        // GBR-04: Đính kèm toàn bộ các vai trò (Multi-Role) của người dùng vào JWT Claims
        foreach (var role in roles.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            claims.Add(new Claim(ClaimTypes.Role, role.ToUpperInvariant()));
            claims.Add(new Claim("roles", role.ToUpperInvariant()));
        }

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var tokenDescriptor = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            notBefore: DateTime.UtcNow,
            expires: expiresAt,
            signingCredentials: credentials);

        var rawAccessToken = new JwtSecurityTokenHandler().WriteToken(tokenDescriptor);
        return (rawAccessToken, expiresAt);
    }

    /// <inheritdoc />
    public (string RawToken, string TokenHash, DateTime ExpiresAt) GenerateRefreshToken(bool rememberMe = false)
    {
        var randomBytes = RandomNumberGenerator.GetBytes(64);
        var rawToken = Convert.ToBase64String(randomBytes)
            .Replace("+", "-")
            .Replace("/", "_")
            .TrimEnd('=');

        var tokenHash = ComputeSha256Hash(rawToken);

        // UC-01: Mặc định 7 ngày, hoặc 30 ngày nếu người dùng chọn Remember Me
        var daysToLive = rememberMe ? 30 : 7;
        var expiresAt = DateTime.UtcNow.AddDays(daysToLive);

        return (rawToken, tokenHash, expiresAt);
    }

    /// <inheritdoc />
    public string ComputeSha256Hash(string rawValue)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(rawValue.Trim()));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    /// <inheritdoc />
    public string GenerateSixDigitOtp()
    {
        var value = RandomNumberGenerator.GetInt32(100000, 1000000);
        return value.ToString("D6");
    }
}
