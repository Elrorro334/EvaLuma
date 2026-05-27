using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace Rodnix.EvaLuma.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly EvalumaDbContext _context;
    private readonly IConfiguration _configuration;

    public AuthController(EvalumaDbContext context, IConfiguration configuration)
    {
        _context = context;
        _configuration = configuration;
    }

    [HttpPost("login/sso")]
    public async Task<IActionResult> SsoLogin([FromBody] SsoLoginRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.EmailCorporativo) || string.IsNullOrWhiteSpace(request.SsoIdentificador))
        {
            return BadRequest(new { Message = "El payload de SSO está incompleto. Faltan credenciales de identificación." });
        }

        var usuario = await _context.Usuarios
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.EmailCorporativo == request.EmailCorporativo && u.SsoIdentificador == request.SsoIdentificador);

        if (usuario == null)
        {
            return Unauthorized(new { Message = "Usuario no encontrado en el directorio corporativo de EVALUMA." });
        }

        if (!usuario.Estatus)
        {
            return StatusCode(403, new { Message = "La cuenta de usuario se encuentra inactiva." });
        }

        var token = GenerarJwtToken(usuario);

        return Ok(new
        {
            Token = token,
            Usuario = new
            {
                usuario.IdUsuario,
                usuario.NombreCompleto,
                usuario.EmailCorporativo,
                usuario.Rol,
                usuario.Departamento
            }
        });
    }

    private string GenerarJwtToken(Usuario usuario)
    {
        var jwtSettings = _configuration.GetSection("Jwt");
        var key = Encoding.ASCII.GetBytes(jwtSettings["Key"]!);

        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, usuario.IdUsuario.ToString()),
            new Claim(ClaimTypes.Email, usuario.EmailCorporativo),
            new Claim(ClaimTypes.Name, usuario.NombreCompleto),
            new Claim(ClaimTypes.Role, usuario.Rol)
        };

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Expires = DateTime.UtcNow.AddHours(double.Parse(jwtSettings["ExpireHours"]!)),
            SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature),
            Issuer = jwtSettings["Issuer"],
            Audience = jwtSettings["Audience"]
        };

        var tokenHandler = new JwtSecurityTokenHandler();
        var token = tokenHandler.CreateToken(tokenDescriptor);

        return tokenHandler.WriteToken(token);
    }
}

public class SsoLoginRequest
{
    public string EmailCorporativo { get; set; } = string.Empty;
    public string SsoIdentificador { get; set; } = string.Empty;
}

public class Usuario
{
    public int IdUsuario { get; set; }
    public string SsoIdentificador { get; set; } = string.Empty;
    public string NombreCompleto { get; set; } = string.Empty;
    public string EmailCorporativo { get; set; } = string.Empty;
    public string Rol { get; set; } = string.Empty;
    public string Departamento { get; set; } = string.Empty;
    public DateTime FechaRegistro { get; set; }
    public bool Estatus { get; set; }
}

public class EvalumaDbContext : DbContext
{
    public EvalumaDbContext(DbContextOptions<EvalumaDbContext> options) : base(options) { }
    public DbSet<Usuario> Usuarios { get; set; }
}