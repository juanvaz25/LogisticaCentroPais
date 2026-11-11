using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using LCP.Api.Data;
using LCP.Api.Data.Entities;
using LCP.Api.Services;
using LCP.Shared.DTOs;
using LCP.Shared.Enums;

namespace LCP.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly LcpDbContext _db;
    private readonly ITokenService _tokenService;
    private readonly IEmailService _emailService;
    private readonly IConfiguration _config;

    public AuthController(LcpDbContext db, ITokenService tokenService, IEmailService emailService, IConfiguration config)
    {
        _db = db;
        _tokenService = tokenService;
        _emailService = emailService;
        _config = config;
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<ActionResult<LoginResponseDto>> Login([FromBody] LoginRequestDto request)
    {
        if (!ModelState.IsValid)
            return BadRequest(new LoginResponseDto { Exitoso = false, Mensaje = "Datos inválidos." });

        var usuario = await _db.Usuarios
            .Include(u => u.ClienteEmpresa)
            .FirstOrDefaultAsync(u => u.Email.ToLower() == request.Email.ToLower());

        if (usuario == null || !DbInitializer.VerifyPassword(request.Password, usuario.PasswordHash))
        {
            return Unauthorized(new LoginResponseDto
            {
                Exitoso = false,
                Mensaje = "Credenciales incorrectas. Verifique el correo y la contraseña."
            });
        }

        if (!usuario.Activo)
        {
            return Unauthorized(new LoginResponseDto
            {
                Exitoso = false,
                Mensaje = "El usuario se encuentra inactivo. Contacte al Administrador."
            });
        }

        if (!usuario.EmailConfirmado)
        {
            return Unauthorized(new LoginResponseDto
            {
                Exitoso = false,
                Mensaje = "Su correo electrónico aún no ha sido verificado. Por favor revise el mensaje de activación que enviamos a su casilla."
            });
        }

        var token = _tokenService.GenerarToken(usuario);

        var usuarioDto = new UsuarioDto
        {
            Id = usuario.Id,
            Nombre = usuario.Nombre,
            Apellido = usuario.Apellido,
            Email = usuario.Email,
            Telefono = usuario.Telefono,
            Rol = usuario.Rol,
            Activo = usuario.Activo,
            DebeCambiarPassword = usuario.DebeCambiarPassword,
            ClienteId = usuario.ClienteId,
            EmpresaRazonSocial = usuario.ClienteEmpresa?.RazonSocial,
            FechaAlta = usuario.FechaAlta
        };

        return Ok(new LoginResponseDto
        {
            Exitoso = true,
            Mensaje = "Inicio de sesión exitoso.",
            Token = token,
            Expiracion = DateTime.UtcNow.AddDays(7),
            Usuario = usuarioDto
        });
    }

    [HttpGet("me")]
    [Authorize]
    public async Task<ActionResult<UsuarioDto>> GetCurrentUser()
    {
        var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(userIdStr, out int userId))
            return Unauthorized();

        var usuario = await _db.Usuarios
            .Include(u => u.ClienteEmpresa)
            .FirstOrDefaultAsync(u => u.Id == userId);

        if (usuario == null)
            return NotFound();

        return Ok(new UsuarioDto
        {
            Id = usuario.Id,
            Nombre = usuario.Nombre,
            Apellido = usuario.Apellido,
            Email = usuario.Email,
            Telefono = usuario.Telefono,
            Rol = usuario.Rol,
            Activo = usuario.Activo,
            ClienteId = usuario.ClienteId,
            EmpresaRazonSocial = usuario.ClienteEmpresa?.RazonSocial,
            FechaAlta = usuario.FechaAlta
        });
    }

    [HttpPost("cambiar-password")]
    [Authorize]
    public async Task<ActionResult> CambiarPassword([FromBody] CambiarPasswordDto dto)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(userIdStr, out int userId))
            return Unauthorized();

        var usuario = await _db.Usuarios.FindAsync(userId);
        if (usuario == null)
            return NotFound();

        if (!DbInitializer.VerifyPassword(dto.PasswordActual, usuario.PasswordHash))
        {
            return BadRequest(new { Mensaje = "La contraseña actual ingresada es incorrecta." });
        }

        if (dto.PasswordActual == dto.PasswordNuevo)
        {
            return BadRequest(new { Mensaje = "La nueva contraseña debe ser diferente a la contraseña actual." });
        }

        usuario.PasswordHash = DbInitializer.HashPassword(dto.PasswordNuevo);
        usuario.DebeCambiarPassword = false;
        await _db.SaveChangesAsync();

        return Ok(new { Mensaje = "Contraseña actualizada exitosamente." });
    }

    [HttpPost("registro-empresa")]
    [AllowAnonymous]
    public async Task<ActionResult> RegistrarEmpresa([FromBody] RegistroEmpresaDto dto)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var empresaExistente = await _db.ClientesEmpresa.FirstOrDefaultAsync(c => c.CUIT == dto.CUIT);
        if (empresaExistente != null)
        {
            if (empresaExistente.EstadoAlta == EstadoPostulacionAlta.PendienteConfirmacionEmail && 
                empresaExistente.EmailContacto.ToLower() == dto.EmailContacto.ToLower())
            {
                // Reenviar correo de confirmación
                var webBase = _config["SmtpSettings:WebBaseUrl"] ?? "http://localhost:5147";
                var urlReenvio = $"{webBase.TrimEnd('/')}/Auth/ConfirmarEmail?token={empresaExistente.TokenConfirmacionEmail}";
                _ = _emailService.SendConfirmacionEmailRegistroAsync(dto.EmailContacto, dto.RazonSocial, urlReenvio);

                return Ok(new { Mensaje = "Ya existía una solicitud para este CUIT pendiente de confirmación de email. Hemos reenviado el correo de verificación a su casilla." });
            }

            return Conflict(new { Mensaje = "Ya existe una empresa registrada con ese CUIT." });
        }

        if (await _db.Usuarios.AnyAsync(u => u.Email.ToLower() == dto.EmailContacto.ToLower()))
            return Conflict(new { Mensaje = "El correo de contacto ya está registrado como usuario en el sistema." });

        var tokenConfirmacion = Guid.NewGuid().ToString("N");

        var empresa = new ClienteEmpresa
        {
            RazonSocial = dto.RazonSocial,
            CUIT = dto.CUIT,
            Rubro = dto.Rubro,
            Domicilio = dto.Domicilio,
            Localidad = dto.Localidad,
            Provincia = dto.Provincia,
            EmailContacto = dto.EmailContacto,
            TelefonoContacto = dto.TelefonoContacto,
            EstadoAlta = EstadoPostulacionAlta.PendienteConfirmacionEmail,
            EmailConfirmado = false,
            TokenConfirmacionEmail = tokenConfirmacion,
            FechaSolicitud = DateTime.UtcNow
        };

        _db.ClientesEmpresa.Add(empresa);
        await _db.SaveChangesAsync();

        // Se crea el usuario en estado inactivo hasta que el Administrador apruebe la empresa y genere las credenciales
        var usuario = new Usuario
        {
            Nombre = dto.RazonSocial,
            Apellido = "(Contacto)",
            Email = dto.EmailContacto,
            PasswordHash = DbInitializer.HashPassword(Guid.NewGuid().ToString("N")), // Clave inaccesible hasta aprobación del admin
            Telefono = dto.TelefonoContacto,
            Rol = RolUsuario.Cliente,
            ClienteId = empresa.Id,
            Activo = false, // Inactivo hasta que el Administrador lo habilite
            DebeCambiarPassword = true, // Obligado a cambiar en su primer ingreso tras recibir sus credenciales
            FechaAlta = DateTime.UtcNow
        };

        _db.Usuarios.Add(usuario);
        await _db.SaveChangesAsync();

        // Enviar correo electrónico de verificación de dirección con botón de confirmación
        var webUrl = _config["SmtpSettings:WebBaseUrl"] ?? "http://localhost:5147";
        var confirmUrl = $"{webUrl.TrimEnd('/')}/Auth/ConfirmarEmail?token={tokenConfirmacion}";
        _ = _emailService.SendConfirmacionEmailRegistroAsync(dto.EmailContacto, dto.RazonSocial, confirmUrl);

        return Ok(new { Mensaje = "Solicitud de alta recibida exitosamente. Hemos enviado un correo a su casilla para verificar su dirección antes de remitir los datos a la administración." });
    }

    [HttpGet("confirmar-email")]
    [AllowAnonymous]
    public async Task<ActionResult<ConfirmarEmailResponseDto>> ConfirmarEmail([FromQuery] string token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return BadRequest(new ConfirmarEmailResponseDto
            {
                Exitoso = false,
                Mensaje = "El token de confirmación es obligatorio y no fue provisto."
            });
        }

        // 1. Validar si corresponde a una Empresa Cliente
        var empresa = await _db.ClientesEmpresa.FirstOrDefaultAsync(c => c.TokenConfirmacionEmail == token);
        if (empresa != null)
        {
            if (empresa.EmailConfirmado)
            {
                return Ok(new ConfirmarEmailResponseDto
                {
                    Exitoso = true,
                    YaConfirmado = true,
                    RazonSocial = empresa.RazonSocial,
                    Mensaje = "Este correo electrónico ya había sido verificado previamente. Su solicitud se encuentra pendiente de revisión administrativa."
                });
            }

            empresa.EmailConfirmado = true;
            empresa.FechaConfirmacionEmail = DateTime.UtcNow;
            empresa.EstadoAlta = EstadoPostulacionAlta.PendienteRevision;
            await _db.SaveChangesAsync();

            return Ok(new ConfirmarEmailResponseDto
            {
                Exitoso = true,
                YaConfirmado = false,
                RazonSocial = empresa.RazonSocial,
                Mensaje = "¡Correo electrónico verificado exitosamente! Su solicitud de apertura de cuenta ya fue remitida a la Bandeja de Aprobaciones del Administrador de LCP."
            });
        }

        // 2. Validar si corresponde a un Conductor / Chofer postulante
        var conductor = await _db.Conductores.FirstOrDefaultAsync(c => c.TokenConfirmacionEmail == token);
        if (conductor != null)
        {
            if (conductor.EmailConfirmado)
            {
                return Ok(new ConfirmarEmailResponseDto
                {
                    Exitoso = true,
                    YaConfirmado = true,
                    RazonSocial = conductor.NombreCompleto,
                    Mensaje = "Tu correo electrónico ya fue validado previamente. Tu postulación se encuentra en la Bandeja del Administrador para su aprobación."
                });
            }

            conductor.EmailConfirmado = true;
            conductor.FechaConfirmacionEmail = DateTime.UtcNow;
            conductor.EstadoPostulacion = EstadoPostulacionAlta.PendienteRevision;
            await _db.SaveChangesAsync();

            return Ok(new ConfirmarEmailResponseDto
            {
                Exitoso = true,
                YaConfirmado = false,
                RazonSocial = conductor.NombreCompleto,
                Mensaje = "¡Correo electrónico validado exitosamente! Tu postulación ahora figura en la Bandeja de Aprobaciones del Administrador de LCP para habilitar tu usuario y enviarte tu contraseña."
            });
        }

        // 3. Validar si corresponde a Personal Interno / Usuario de la plataforma
        var usuario = await _db.Usuarios.FirstOrDefaultAsync(u => u.TokenConfirmacionEmail == token);
        if (usuario != null)
        {
            if (usuario.EmailConfirmado)
            {
                return Ok(new ConfirmarEmailResponseDto
                {
                    Exitoso = true,
                    YaConfirmado = true,
                    RazonSocial = usuario.NombreCompleto,
                    Mensaje = "Tu correo electrónico ya fue validado previamente. Tu cuenta de personal interno se encuentra activa y lista para operar."
                });
            }

            usuario.EmailConfirmado = true;
            usuario.FechaConfirmacionEmail = DateTime.UtcNow;
            usuario.Activo = true;
            await _db.SaveChangesAsync();

            return Ok(new ConfirmarEmailResponseDto
            {
                Exitoso = true,
                YaConfirmado = false,
                RazonSocial = usuario.NombreCompleto,
                Mensaje = "¡Correo electrónico verificado exitosamente! Tu cuenta de personal interno ha quedado formalmente activada. Ya puedes ingresar al sistema con tu correo y la contraseña provisoria recibida."
            });
        }

        return NotFound(new ConfirmarEmailResponseDto
        {
            Exitoso = false,
            Mensaje = "El enlace de confirmación no es válido o ya ha expirado."
        });
    }

    [HttpPost("postulacion-chofer")]
    [AllowAnonymous]
    public async Task<ActionResult> PostulacionChofer([FromBody] PostulacionChoferDto dto)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var conductorExistente = await _db.Conductores.FirstOrDefaultAsync(c => c.CUIL.Trim() == dto.CUIL.Trim());
        if (conductorExistente != null)
        {
            if (conductorExistente.EstadoPostulacion == EstadoPostulacionAlta.PendienteConfirmacionEmail &&
                conductorExistente.Email.ToLower() == dto.Email.ToLower())
            {
                var webBase = _config["SmtpSettings:WebBaseUrl"] ?? "http://localhost:5147";
                var urlReenvio = $"{webBase.TrimEnd('/')}/Auth/ConfirmarEmail?token={conductorExistente.TokenConfirmacionEmail}";
                _ = _emailService.SendConfirmacionEmailChoferAsync(dto.Email, conductorExistente.NombreCompleto, urlReenvio);

                return Ok(new { Mensaje = "Ya existía una postulación pendiente de confirmación de email. Hemos reenviado el correo de verificación a tu casilla." });
            }

            return Conflict(new { Mensaje = "Ya existe un chofer registrado con ese CUIL." });
        }

        if (await _db.Usuarios.AnyAsync(u => u.Email.ToLower() == dto.Email.ToLower()))
        {
            return Conflict(new { Mensaje = "El correo electrónico ya se encuentra registrado como usuario en el sistema." });
        }

        var tokenConfirmacion = Guid.NewGuid().ToString("N");

        var conductor = new Conductor
        {
            TipoConductor = dto.TipoConductor,
            ClienteEmpresaId = dto.ClienteEmpresaId,
            Nombre = dto.Nombre,
            Apellido = dto.Apellido,
            CUIL = dto.CUIL,
            FechaNacimiento = dto.FechaNacimiento,
            CelularWhatsApp = dto.CelularWhatsApp,
            Email = dto.Email,
            Domicilio = dto.Domicilio,
            Localidad = dto.Localidad,
            Provincia = dto.Provincia,
            PatenteCamion = dto.PatenteCamion?.ToUpper().Trim() ?? string.Empty,
            CapacidadCargaKg = dto.CapacidadCargaKg,
            CapacidadVolumenM3 = dto.CapacidadVolumenM3,
            EstadoPostulacion = EstadoPostulacionAlta.PendienteConfirmacionEmail,
            DeclaracionJuradaAceptada = dto.DeclaracionJuradaAceptada,
            EmailConfirmado = false,
            TokenConfirmacionEmail = tokenConfirmacion,
            LicenciaArchivoUrl = dto.LicenciaArchivoUrl,
            SeguroCamionArchivoUrl = dto.SeguroCamionArchivoUrl,
            FechaPostulacion = DateTime.UtcNow
        };

        _db.Conductores.Add(conductor);
        await _db.SaveChangesAsync();

        // Vincular los documentos adjuntos cargados a este conductor
        if (!string.IsNullOrEmpty(dto.LicenciaArchivoUrl))
        {
            var docLic = await _db.DocumentosAdjuntos.FirstOrDefaultAsync(d => d.RutaUrl == dto.LicenciaArchivoUrl);
            if (docLic != null)
            {
                docLic.ConductorId = conductor.Id;
            }
            else
            {
                _db.DocumentosAdjuntos.Add(new DocumentoAdjunto
                {
                    TipoDocumento = "LicenciaConducir",
                    NombreArchivo = System.IO.Path.GetFileName(dto.LicenciaArchivoUrl),
                    RutaUrl = dto.LicenciaArchivoUrl,
                    ConductorId = conductor.Id,
                    SubidoPorUsuarioId = 1,
                    FechaSubida = DateTime.UtcNow
                });
            }
        }

        if (!string.IsNullOrEmpty(dto.SeguroCamionArchivoUrl))
        {
            var docSeg = await _db.DocumentosAdjuntos.FirstOrDefaultAsync(d => d.RutaUrl == dto.SeguroCamionArchivoUrl);
            if (docSeg != null)
            {
                docSeg.ConductorId = conductor.Id;
            }
            else
            {
                _db.DocumentosAdjuntos.Add(new DocumentoAdjunto
                {
                    TipoDocumento = "SeguroCamion",
                    NombreArchivo = System.IO.Path.GetFileName(dto.SeguroCamionArchivoUrl),
                    RutaUrl = dto.SeguroCamionArchivoUrl,
                    ConductorId = conductor.Id,
                    SubidoPorUsuarioId = 1,
                    FechaSubida = DateTime.UtcNow
                });
            }
        }

        await _db.SaveChangesAsync();

        // Enviar correo de confirmación de email al chofer postulante
        var webUrl = _config["SmtpSettings:WebBaseUrl"] ?? "http://localhost:5147";
        var confirmUrl = $"{webUrl.TrimEnd('/')}/Auth/ConfirmarEmail?token={tokenConfirmacion}";
        _ = _emailService.SendConfirmacionEmailChoferAsync(dto.Email, conductor.NombreCompleto, confirmUrl);

        return Ok(new { Mensaje = "Postulación enviada exitosamente. Te enviamos un correo para verificar tu casilla antes de remitir los datos a revisión administrativa." });
    }
}
