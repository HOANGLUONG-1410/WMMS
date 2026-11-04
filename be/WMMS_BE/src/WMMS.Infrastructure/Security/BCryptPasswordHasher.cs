using WMMS.Application.Interfaces.Services;

namespace WMMS.Infrastructure.Security;

/// <summary>
/// Triển khai Băm và Kiểm tra mật khẩu bằng thuật toán BCrypt (BR-SEC-01).
/// Sử dụng Work Factor = 12 theo đúng đặc tả bảo mật Report 3 &amp; Report 4.
/// </summary>
public class BCryptPasswordHasher : IPasswordHasher
{
    private const int WorkFactor = 12;

    /// <inheritdoc />
    public string HashPassword(string plainPassword)
    {
        return BCrypt.Net.BCrypt.HashPassword(plainPassword, workFactor: WorkFactor);
    }

    /// <inheritdoc />
    public bool VerifyPassword(string plainPassword, string passwordHash)
    {
        if (string.IsNullOrWhiteSpace(plainPassword) || string.IsNullOrWhiteSpace(passwordHash))
        {
            return false;
        }

        try
        {
            return BCrypt.Net.BCrypt.Verify(plainPassword, passwordHash);
        }
        catch
        {
            return false;
        }
    }
}
