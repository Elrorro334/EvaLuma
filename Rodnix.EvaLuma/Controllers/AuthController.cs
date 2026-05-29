using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Rodnix.EvaLuma.Data;
using Rodnix.EvaLuma.Models;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

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

    [HttpGet("test-db")]
    public async Task<IActionResult> TestDbConnection()
    {
        try
        {
            var count = await _context.Usuarios.CountAsync();

            return Ok(new
            {
                Estatus = "Conexión exitosa",
                TotalUsuarios = count,
                BaseDeDatos = _context.Database.GetDbConnection().Database
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fallo al intentar conectar con la base de datos.");

            return StatusCode(500, new
            {
                Estatus = "Error de conexión",
                Detalle = ex.Message
            });
        }
    }

    [HttpGet("usuarios")]
    [Authorize]
    public async Task<IActionResult> ObtenerUsuarios()
    {
        try
        {
            var usuarios = await _context.Usuarios
                .AsNoTracking()
                .Select(u => new
                {
                    u.IdUsuario,
                    u.NombreCompleto,
                    u.EmailCorporativo,
                    u.Rol,
                    u.Departamento,
                    u.Estatus,
                    u.FechaRegistro
                })
                .ToListAsync();

            return Ok(usuarios);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fallo al intentar obtener la lista de usuarios.");

            return StatusCode(500, new
            {
                Error = "Error interno del servidor al consultar el directorio."
            });
        }
    }

    [HttpPost("registro")]
    [Authorize(Roles = "Administrador")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> RegistrarUsuario([FromBody] RegistroUsuarioRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.EmailCorporativo) ||
            string.IsNullOrWhiteSpace(request.SsoIdentificador) ||
            string.IsNullOrWhiteSpace(request.NombreCompleto) ||
            string.IsNullOrWhiteSpace(request.Rol))
        {
            return BadRequest(new { Error = "Petición inválida. Faltan campos obligatorios." });
        }

        var rolesPermitidos = new[] { "Empleado", "Auditor", "Administrador" };
        if (!rolesPermitidos.Contains(request.Rol))
        {
            return BadRequest(new { Error = "El rol especificado no es válido para esta corporación." });
        }

        var existeUsuario = await _context.Usuarios
            .AsNoTracking()
            .AnyAsync(u => u.EmailCorporativo == request.EmailCorporativo || u.SsoIdentificador == request.SsoIdentificador);

        if (existeUsuario)
        {
            return BadRequest(new { Error = "El correo corporativo o el identificador SSO ya se encuentran registrados en el sistema." });
        }

        var nuevoUsuario = new Usuario
        {
            SsoIdentificador = request.SsoIdentificador,
            NombreCompleto = request.NombreCompleto,
            EmailCorporativo = request.EmailCorporativo,
            Rol = request.Rol,
            Departamento = request.Departamento ?? string.Empty,
            FechaRegistro = DateTime.UtcNow,
            Estatus = true
        };

        _context.Usuarios.Add(nuevoUsuario);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Nuevo usuario aprovisionado: {IdUsuario} por el administrador: {AdminId}",
            nuevoUsuario.IdUsuario,
            User.FindFirst(ClaimTypes.NameIdentifier)?.Value);

        return StatusCode(201, new
        {
            Mensaje = "Usuario creado exitosamente.",
            Usuario = new
            {
                nuevoUsuario.IdUsuario,
                nuevoUsuario.NombreCompleto,
                nuevoUsuario.EmailCorporativo,
                nuevoUsuario.Rol
            }
        });
    }

    [HttpGet("perfil")]
    [Authorize]
    public async Task<IActionResult> GetPerfil()
    {
        var emailClaim = User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value;

        if (string.IsNullOrEmpty(emailClaim))
        {
            return Unauthorized(new { error = "El token no contiene un identificador válido." });
        }

        var perfil = await _context.Usuarios
            .Where(u => u.EmailCorporativo == emailClaim)
            .Select(u => new
            {
                nombreCompleto = u.NombreCompleto,
                emailCorporativo = u.EmailCorporativo,
                rol = u.Rol,
                departamento = u.Departamento,
                estatus = u.Estatus,
                fechaRegistro = u.FechaRegistro
            })
            .FirstOrDefaultAsync();

        if (perfil == null)
        {
            return NotFound(new { error = "Usuario no encontrado en el directorio activo." });
        }

        return Ok(perfil);
    }

    // DTO para recibir los datos de actualización
    public class ActualizarPerfilDto
    {
        public string NombreCompleto { get; set; }
        public string Departamento { get; set; }
    }

    [HttpPut("perfil")]
    [Authorize]
    public async Task<IActionResult> ActualizarPerfil([FromBody] ActualizarPerfilDto dto)
    {
        // Extraemos la identidad del JWT
        var emailClaim = User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value;

        if (string.IsNullOrEmpty(emailClaim))
        {
            return Unauthorized(new { error = "El token no contiene un identificador válido." });
        }

        // Buscamos el registro real en la base de datos
        var usuario = await _context.Usuarios
            .FirstOrDefaultAsync(u => u.EmailCorporativo == emailClaim);

        if (usuario == null)
        {
            return NotFound(new { error = "Usuario no encontrado en el directorio activo." });
        }

        // Actualizamos solo los campos permitidos y validamos que no vengan vacíos
        if (!string.IsNullOrWhiteSpace(dto.NombreCompleto))
        {
            usuario.NombreCompleto = dto.NombreCompleto.Trim();
        }

        if (!string.IsNullOrWhiteSpace(dto.Departamento))
        {
            usuario.Departamento = dto.Departamento.Trim();
        }

        try
        {
            _context.Usuarios.Update(usuario);
            await _context.SaveChangesAsync();

            return Ok(new { mensaje = "Perfil actualizado correctamente." });
        }
        catch (Exception ex)
        {
            // Útil para debuggear problemas de concurrencia o restricciones de base de datos
            return StatusCode(500, new { error = "Error interno al guardar los cambios en la base de datos." });
        }
    }

    public class RegistroUsuarioRequest
    {
        public string SsoIdentificador { get; set; } = string.Empty;
        public string NombreCompleto { get; set; } = string.Empty;
        public string EmailCorporativo { get; set; } = string.Empty;
        public string Rol { get; set; } = string.Empty;
        public string? Departamento { get; set; }
    }

    public class SsoLoginRequest
    {
        public string EmailCorporativo { get; set; } = string.Empty;
        public string SsoIdentificador { get; set; } = string.Empty;
    }
}