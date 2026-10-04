namespace Nutriologo.Api.Auth;

using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

/// <summary>Emite JWT HMAC-SHA256 con sub = Id de usuario. Clave vía Auth__Key.</summary>
public sealed class JwtIssuer(IConfiguration config)
{
    private readonly byte[] _key = Encoding.UTF8.GetBytes(
        config["Auth:Key"] ?? throw new InvalidOperationException("Falta Auth:Key (variable Auth__Key)."));
    private readonly string _issuer = config["Auth:Issuer"] ?? "nutriologo";

    public string Issue(int userId, string correo)
    {
        var handler = new JwtSecurityTokenHandler();
        var desc = new SecurityTokenDescriptor
        {
            Issuer = _issuer,
            Audience = _issuer,
            Subject = new ClaimsIdentity(new[]
            {
                new Claim(JwtRegisteredClaimNames.Sub, userId.ToString()),
                new Claim(JwtRegisteredClaimNames.Email, correo),
            }),
            Expires = DateTime.UtcNow.AddHours(12),
            SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(_key), SecurityAlgorithms.HmacSha256),
        };
        return handler.WriteToken(handler.CreateToken(desc));
    }

    public static int UserId(ClaimsPrincipal user)
    {
        var sub = user.FindFirstValue(JwtRegisteredClaimNames.Sub) ?? user.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(sub, out var id) ? id : throw new UnauthorizedAccessException("Token sin sub.");
    }
}
