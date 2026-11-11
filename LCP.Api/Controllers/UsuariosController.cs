using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using LCP.Api.Data;
using LCP.Api.Data.Entities;
using LCP.Shared.Constants;
using LCP.Shared.DTOs;
using LCP.Shared.Enums;

namespace LCP.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Administrador,Operador")]
public class UsuariosController : ControllerBase
{
    private readonly LcpDbContext _db;
    private readonly Services.IEmailService _emailService;
    private readonly IConfiguration _config;

    public UsuariosController(LcpDbContext db, Services.IEmailService emailService, IConfiguration config)
    {
        _db = db;
        _emailService = emailService;
        _config = config;
    }

    [HttpGet]
    public async Task<ActionResult<List<UsuarioDto>>> GetUsuarios([FromQuery] bool incluirClientes = false)
    {
        var query = _db.Usuarios
            .Include(u => u.ClienteEmpresa)
            .AsQueryable();

        // Por defecto solo lista personal interno (Operadores, Auditores, Administradores, Conductores)
        if (!incluirClientes)
        {
            query = query.Where(u => u.Rol != RolUsuario.Cliente);
        }

        var users = await query
            .OrderBy(u => u.Rol)
            .ThenBy(u => u.Nombre)
            .ToListAsync();

        return Ok(users.Select(u => new UsuarioDto
        {
            Id = u.Id,
            Nombre = u.Nombre,
            Apellido = u.Apellido,
            Email = u.Email,
            Telefono = u.Telefono,
            Rol = u.Rol,
            Activo = u.Activo,
            DebeCambiarPassword = u.DebeCambiarPassword,
            ClienteId = u.ClienteId,
            EmpresaRazonSocial = u.ClienteEmpresa?.RazonSocial,
            FechaAlta = u.FechaAlta,

            Dni = u.Dni,
            FechaNacimiento = u.FechaNacimiento,
            Direccion = u.Direccion,
            Localidad = u.Localidad,
            Provincia = u.Provincia,
            CodigoPostal = u.CodigoPostal,
            EstadoCivil = u.EstadoCivil,
            ContactoEmergenciaNombre = u.ContactoEmergenciaNombre,
            ContactoEmergenciaTelefono = u.ContactoEmergenciaTelefono,
            PuestoCargo = u.PuestoCargo,
            FechaIngreso = u.FechaIngreso,
            ModalidadContratacion = u.ModalidadContratacion,
            ObservacionesLegajo = u.ObservacionesLegajo,
            DocumentoIdentidadUrl = u.DocumentoIdentidadUrl,
            CvArchivoUrl = u.CvArchivoUrl,
            EmailConfirmado = u.EmailConfirmado,
            TokenConfirmacionEmail = u.TokenConfirmacionEmail,
            FechaConfirmacionEmail = u.FechaConfirmacionEmail
        }).ToList());
    }

    [HttpPost]
    [Authorize(Roles = "Administrador")]
    public async Task<ActionResult> CrearUsuarioInterno([FromBody] CrearPersonalDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Email))
        {
            return BadRequest(new { Mensaje = "El correo electrónico es obligatorio." });
        }

        var emailLower = dto.Email.Trim().ToLower();

        // 1. Verificación de correo único en todo el ecosistema (Usuarios, Clientes, Conductores)
        if (await _db.Usuarios.AnyAsync(u => u.Email.ToLower() == emailLower) ||
            await _db.ClientesEmpresa.AnyAsync(c => c.EmailContacto.ToLower() == emailLower) ||
            await _db.Conductores.AnyAsync(c => c.Email.ToLower() == emailLower))
        {
            return Conflict(new { Mensaje = $"El correo electrónico '{dto.Email}' ya está registrado en el sistema. Debe ser único." });
        }

        // 2. Verificación de DNI / CUIL único si fue provisto
        if (!string.IsNullOrWhiteSpace(dto.Dni))
        {
            var dniLimpio = dto.Dni.Trim().Replace(".", "").Replace("-", "");
            if (await _db.Usuarios.AnyAsync(u => u.Dni != null && u.Dni.Replace(".", "").Replace("-", "") == dniLimpio))
            {
                return Conflict(new { Mensaje = $"Ya existe personal registrado con el documento / CUIL '{dto.Dni}'." });
            }
        }

        var passwordProvisoria = DbInitializer.GenerarPasswordProvisoria("LCP");
        var tokenConfirmacion = Guid.NewGuid().ToString("N");

        var nuevo = new Usuario
        {
            Nombre = dto.Nombre.Trim(),
            Apellido = dto.Apellido.Trim(),
            Email = emailLower,
            PasswordHash = DbInitializer.HashPassword(passwordProvisoria),
            Telefono = dto.Telefono.Trim(),
            Rol = dto.Rol,
            Activo = false, // Inactivo hasta que confirme su casilla verídica
            DebeCambiarPassword = true,
            FechaAlta = DateTime.UtcNow,

            Dni = dto.Dni?.Trim(),
            FechaNacimiento = dto.FechaNacimiento,
            Direccion = dto.Direccion?.Trim(),
            Localidad = dto.Localidad?.Trim(),
            Provincia = dto.Provincia?.Trim(),
            CodigoPostal = dto.CodigoPostal?.Trim(),
            EstadoCivil = dto.EstadoCivil?.Trim(),
            ContactoEmergenciaNombre = dto.ContactoEmergenciaNombre?.Trim(),
            ContactoEmergenciaTelefono = dto.ContactoEmergenciaTelefono?.Trim(),
            PuestoCargo = string.IsNullOrWhiteSpace(dto.PuestoCargo) ? dto.Rol.ToString() : dto.PuestoCargo.Trim(),
            FechaIngreso = dto.FechaIngreso,
            ModalidadContratacion = dto.ModalidadContratacion?.Trim(),
            ObservacionesLegajo = dto.ObservacionesLegajo?.Trim(),
            DocumentoIdentidadUrl = dto.DocumentoIdentidadUrl,
            CvArchivoUrl = dto.CvArchivoUrl,

            EmailConfirmado = false,
            TokenConfirmacionEmail = tokenConfirmacion,
            FechaConfirmacionEmail = null
        };

        _db.Usuarios.Add(nuevo);
        await _db.SaveChangesAsync();

        // Enviar email de confirmación y credenciales
        var webUrl = _config["SmtpSettings:WebBaseUrl"] ?? "http://localhost:5147";
        var confirmUrl = $"{webUrl.TrimEnd('/')}/Auth/ConfirmarEmail?token={tokenConfirmacion}";

        _ = _emailService.SendConfirmacionPersonalAsync(
            nuevo.Email,
            nuevo.NombreCompleto,
            nuevo.PuestoCargo ?? nuevo.Rol.ToString(),
            nuevo.Rol.ToString(),
            passwordProvisoria,
            confirmUrl);

        return Ok(new { Mensaje = $"Personal {nuevo.NombreCompleto} creado correctamente. Se envió un correo a {nuevo.Email} con el enlace de confirmación y sus credenciales.", PasswordProvisoria = passwordProvisoria });
    }

    [HttpPost("{id}/reenviar-confirmacion")]
    [Authorize(Roles = "Administrador")]
    public async Task<ActionResult> ReenviarConfirmacionEmail(int id)
    {
        var usuario = await _db.Usuarios.FindAsync(id);
        if (usuario == null) return NotFound(new { Mensaje = "Usuario no encontrado." });

        if (usuario.EmailConfirmado)
        {
            return BadRequest(new { Mensaje = "Este usuario ya confirmó su correo electrónico previamente." });
        }

        if (string.IsNullOrWhiteSpace(usuario.TokenConfirmacionEmail))
        {
            usuario.TokenConfirmacionEmail = Guid.NewGuid().ToString("N");
            await _db.SaveChangesAsync();
        }

        var passwordProvisoria = DbInitializer.GenerarPasswordProvisoria("LCP");
        usuario.PasswordHash = DbInitializer.HashPassword(passwordProvisoria);
        usuario.DebeCambiarPassword = true;
        await _db.SaveChangesAsync();

        var webUrl = _config["SmtpSettings:WebBaseUrl"] ?? "http://localhost:5147";
        var confirmUrl = $"{webUrl.TrimEnd('/')}/Auth/ConfirmarEmail?token={usuario.TokenConfirmacionEmail}";

        _ = _emailService.SendConfirmacionPersonalAsync(
            usuario.Email,
            usuario.NombreCompleto,
            usuario.PuestoCargo ?? usuario.Rol.ToString(),
            usuario.Rol.ToString(),
            passwordProvisoria,
            confirmUrl);

        return Ok(new { Mensaje = $"Correo de verificación y nuevas credenciales reenviados exitosamente a {usuario.Email}." });
    }

    [HttpPut("{id}/toggle-activo")]
    [Authorize(Roles = "Administrador")]
    public async Task<ActionResult> ToggleActivo(int id)
    {
        var user = await _db.Usuarios.FindAsync(id);
        if (user == null) return NotFound();

        user.Activo = !user.Activo;
        await _db.SaveChangesAsync();

        return Ok(new { Mensaje = $"Usuario {(user.Activo ? "activado" : "desactivado")} exitosamente." });
    }
}
