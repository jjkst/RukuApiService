using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using RukuServiceApi.Models;

namespace RukuServiceApi.Services;

public interface IAuthService
{
    string GenerateJwtToken(User user);
    bool ValidateUser(string email, string uid);
}

public class AuthService : IAuthService
{
    private readonly JwtSettings _jwtSettings;
    private readonly ILogger<AuthService> _logger;

    public AuthService(IOptions<JwtSettings> jwtSettings, ILogger<AuthService> logger)
    {
        _jwtSettings = jwtSettings.Value;
        _logger = logger;
    }

    public string GenerateJwtToken(User user)
    {
        if (string.IsNullOrEmpty(_jwtSettings.SecretKey))
        {
            throw new InvalidOperationException("JWT SecretKey is not configured");
        }

        if (_jwtSettings.SecretKey.Length < 32)
        {
            throw new InvalidOperationException(
                $"JWT SecretKey must be at least 32 characters long. Current length: {_jwtSettings.SecretKey.Length}"
            );
        }

        var tokenHandler = new JwtSecurityTokenHandler();
        var key = Encoding.ASCII.GetBytes(_jwtSettings.SecretKey);

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(
                new[]
                {
                    new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                    new Claim(ClaimTypes.Email, user.Email),
                    new Claim(ClaimTypes.Name, user.DisplayName),
                    new Claim(ClaimTypes.Role, user.Role.ToString()),
                    new Claim("uid", user.Uid),
                }
            ),
            Expires = DateTime.UtcNow.AddMinutes(_jwtSettings.ExpirationMinutes),
            Issuer = _jwtSettings.Issuer,
            Audience = _jwtSettings.Audience,
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(key),
                SecurityAlgorithms.HmacSha256Signature
            ),
        };

        var token = tokenHandler.CreateToken(tokenDescriptor);
        return tokenHandler.WriteToken(token);
    }

    public bool ValidateUser(string email, string uid)
    {
        // In a real application, you would validate against your database
        // For now, we'll do basic validation
        return !string.IsNullOrEmpty(email) && !string.IsNullOrEmpty(uid);
    }
}
