using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using LCP.Api.Data.Entities;

namespace LCP.Api.Services;

public interface ITokenService
{
    string GenerarToken(Usuario usuario);
}

public class TokenService : ITokenService
{
    private readonly IConfiguration _config;

    public TokenService(IConfiguration config)
    {
        _config = config;
    }

    public string GenerarToken(Usuario usuario)
    {
        var secretKey = _config["Jwt:Key"] ?? "LCP_SuperSecret_Jwt_Key_Nodo_Logistico_Centro_Pais_2026_Santa_Rosa!";
        var issuer = _config["Jwt:Issuer"] ?? "LogisticaCentroPais";
        var audience = _config["Jwt:Audience"] ?? "LogisticaCentroPaisClients";

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, usuario.Id.ToString()),
            new(ClaimTypes.Email, usuario.Email),
            new(ClaimTypes.Name, usuario.NombreCompleto),
            new(ClaimTypes.Role, usuario.Rol.ToString()),
            new("RolId", ((int)usuario.Rol).ToString())
        };

        if (usuario.ClienteId.HasValue)
        {
            claims.Add(new Claim("ClienteId", usuario.ClienteId.Value.ToString()));
        }

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(30),
            signingCredentials: creds
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
