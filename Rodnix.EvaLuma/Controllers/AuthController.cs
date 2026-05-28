using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Security.Cryptography;
using Rodnix.EvaLuma.Data;
using Rodnix.EvaLuma.Models;

namespace Rodnix.EvaLuma.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly EvalumaDbContext _context;
    private readonly IConfiguration _configuration;
    private readonly ILogger<AuthController> _logger;

    public AuthController(EvalumaDbContext context, IConfiguration configuration, ILogger<AuthController> logger)
    {
        _context = context;
        _configuration = configuration;
        _logger = logger;
    }

    [HttpPost("login/sso")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> SsoLogin([FromBody] SsoLoginRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.EmailCorporativo) ||
            string.IsNullOrWhiteSpace(request.SsoIdentificador) ||
            request.EmailCorporativo.Length > 150)
        {
            _logger.LogWarning("Intento de inicio de sesión con payload inválido desde IP: {Ip}", HttpContext.Connection.RemoteIpAddress);
            return BadRequest(new { Error = "Petición inválida." });
        }

        var usuario = await _context.Usuarios
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.EmailCorporativo == request.EmailCorporativo);

        if (usuario == null)
        {
            _logger.LogWarning("Fallo de autenticación: Usuario no encontrado para el correo {Email}", request.EmailCorporativo);
            return Unauthorized(new { Error = "Credenciales corporativas inválidas." });
        }

        var inputSsoBytes = Encoding.UTF8.GetBytes(request.SsoIdentificador);
        var dbSsoBytes = Encoding.UTF8.GetBytes(usuario.SsoIdentificador);

        if (inputSsoBytes.Length != dbSsoBytes.Length || !CryptographicOperations.FixedTimeEquals(inputSsoBytes, dbSsoBytes))
        {
            _logger.LogWarning("Fallo de autenticación: Identificador SSO incorrecto para {Email}", request.EmailCorporativo);
            return Unauthorized(new { Error = "Credenciales corporativas inválidas." });
        }

        if (!usuario.Estatus)
        {
            _logger.LogWarning("Intento de acceso de usuario inactivo: {IdUsuario}", usuario.IdUsuario);
            return Unauthorized(new { Error = "Credenciales corporativas inválidas." });
        }

        var token = GenerarJwtToken(usuario);

        _logger.LogInformation("Inicio de sesión SSO exitoso para usuario: {IdUsuario}", usuario.IdUsuario);

        return Ok(new
        {
            Token = token,
            ExpiraEn = DateTime.UtcNow.AddHours(double.Parse(_configuration["Jwt:ExpireHours"]!)),
            Usuario = new
            {
                usuario.IdUsuario,
                usuario.NombreCompleto,
                usuario.Rol,
                usuario.Departamento
            }
        });
    }

    private string GenerarJwtToken(Usuario usuario)
    {
        var key = Encoding.UTF8.GetBytes(_configuration["Jwt:Key"]!);

        var claims = new List<Claim>
        {
            new Claim(JwtRegisteredClaimNames.Sub, usuario.IdUsuario.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, usuario.EmailCorporativo),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new Claim(ClaimTypes.Role, usuario.Rol)
        };

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Expires = DateTime.UtcNow.AddHours(double.Parse(_configuration["Jwt:ExpireHours"]!)),
            NotBefore = DateTime.UtcNow,
            SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature),
            Issuer = _configuration["Jwt:Issuer"],
            Audience = _configuration["Jwt:Audience"]
        };

        var tokenHandler = new JwtSecurityTokenHandler();
        var token = tokenHandler.CreateToken(tokenDescriptor);

        return tokenHandler.WriteToken(token);
    }

    public class SsoLoginRequest
    {
        public string EmailCorporativo { get; set; } = string.Empty;
        public string SsoIdentificador { get; set; } = string.Empty;
    }
}