using Microsoft.IdentityModel.Tokens;
using pqy_server.Models.Order;
using pqy_server.Constants;
using pqy_server.Data;
using pqy_server.Models.Users;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

public class TokenService : ITokenService
{
    private readonly IConfiguration _config;
    private readonly AppDbContext _context;
    private readonly string _jwtSecret;
    private readonly string _issuer;
    private readonly string _audience;
    private readonly double _expiryMinutes;

    public TokenService(IConfiguration config, AppDbContext context)
    {
        _config = config;
        _context = context;

        _jwtSecret = Environment.GetEnvironmentVariable("JWT_SECRET")
                     ?? _config["JwtSettings:Key"];

        if (string.IsNullOrEmpty(_jwtSecret) || _jwtSecret.Length < 32)
            throw new InvalidOperationException("JWT signing secret is missing or insecure. It must be at least 32 characters.");

        _issuer = _config["JwtSettings:Issuer"] ?? throw new InvalidOperationException("JWT issuer is not configured.");
        _audience = _config["JwtSettings:Audience"] ?? throw new InvalidOperationException("JWT audience is not configured.");

        if (!double.TryParse(_config["JwtSettings:ExpiresInMinutes"], out _expiryMinutes))
            throw new InvalidOperationException("JWT expiry configuration missing or invalid (ExpiresInMinutes).");
    }

    public string CreateAccessToken(User user)
    {
        var roleName = user.RoleId switch
        {
            1 => RoleConstant.Admin,
            2 => RoleConstant.User,
            _ => RoleConstant.User
        };

        var hasActiveOrder = _context.Orders
            .Where(o => o.UserId == user.UserId && o.Status == OrderStatus.Paid)
            .Any(o => o.ExpiresAt.HasValue && o.ExpiresAt > DateTime.UtcNow);

        var claims = new List<Claim>
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.UserId.ToString()),
            new Claim("userId", user.UserId.ToString()),
            new Claim("username", user.Username ?? string.Empty),
            new Claim(JwtRegisteredClaimNames.Email, user.UserEmail ?? string.Empty),
            new Claim(ClaimTypes.Role, roleName),
            new Claim("isPremium", hasActiveOrder.ToString().ToLower()),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtSecret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _issuer,
            audience: _audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(_expiryMinutes),
            signingCredentials: creds
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public string CreateRefreshToken()
    {
        return Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
    }
}
