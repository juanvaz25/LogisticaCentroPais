using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using LCP.Api.Data;
using LCP.Api.Data.Entities;
using LCP.Api.Services;
using LCP.Shared.DTOs;

namespace LCP.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DocumentosController : ControllerBase
{
    private readonly LcpDbContext _db;
    private readonly IFileStorageService _fileStorage;

    public DocumentosController(LcpDbContext db, IFileStorageService fileStorage)
    {
        _db = db;
        _fileStorage = fileStorage;
    }

    [HttpPost("subir")]
    [AllowAnonymous] // Chofer applicants may upload without JWT during registration
    public async Task<ActionResult<DocumentoAdjuntoDto>> SubirDocumento(
        [FromForm] IFormFile archivo,
        [FromForm] string tipoDocumento,
        [FromForm] int? solicitudOperacionId,
        [FromForm] int? liquidacionId,
        [FromForm] int? conductorId)
    {
        if (archivo == null || archivo.Length == 0)
        {
            return BadRequest(new { Mensaje = "No se ha seleccionado ningún archivo." });
        }

        // Validar tamaño (hasta 10MB según requerimientos)
        if (archivo.Length > 10 * 1024 * 1024)
        {
            return BadRequest(new { Mensaje = "El archivo supera el tamaño máximo permitido de 10 MB." });
        }

        using var stream = archivo.OpenReadStream();
        var rutaUrl = await _fileStorage.GuardarArchivoAsync(stream, archivo.FileName, tipoDocumento.ToLower());

        int subidoPorId = 1; // Default Admin
        var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (int.TryParse(userIdStr, out int uid))
        {
            subidoPorId = uid;
        }

        // Si se subió asociado a una Solicitud de Operación pero sin LiquidacionId, vincularlo a su Liquidación si ya fue generada
        if (!liquidacionId.HasValue && solicitudOperacionId.HasValue)
        {
            var liq = await _db.Liquidaciones.FirstOrDefaultAsync(l => l.SolicitudOperacionId == solicitudOperacionId.Value);
            if (liq != null)
            {
                liquidacionId = liq.Id;
            }
        }
        else if (liquidacionId.HasValue && !solicitudOperacionId.HasValue)
        {
            var liq = await _db.Liquidaciones.FirstOrDefaultAsync(l => l.Id == liquidacionId.Value);
            if (liq?.SolicitudOperacionId != null)
            {
                solicitudOperacionId = liq.SolicitudOperacionId;
            }
        }

        var doc = new DocumentoAdjunto
        {
            TipoDocumento = tipoDocumento,
            NombreArchivo = archivo.FileName,
            RutaUrl = rutaUrl,
            SolicitudOperacionId = solicitudOperacionId,
            LiquidacionId = liquidacionId,
            ConductorId = conductorId,
            SubidoPorUsuarioId = subidoPorId,
            FechaSubida = DateTime.UtcNow
        };

        _db.DocumentosAdjuntos.Add(doc);
        await _db.SaveChangesAsync();

        return Ok(new DocumentoAdjuntoDto
        {
            Id = doc.Id,
            TipoDocumento = doc.TipoDocumento,
            NombreArchivo = doc.NombreArchivo,
            RutaUrl = doc.RutaUrl,
            SolicitudOperacionId = doc.SolicitudOperacionId,
            LiquidacionId = doc.LiquidacionId,
            ConductorId = doc.ConductorId,
            SubidoPorUsuarioId = doc.SubidoPorUsuarioId,
            FechaSubida = doc.FechaSubida
        });
    }

    [HttpGet("liquidacion/{liquidacionId}")]
    public async Task<ActionResult<List<DocumentoAdjuntoDto>>> GetPorLiquidacion(int liquidacionId)
    {
        var liq = await _db.Liquidaciones.FirstOrDefaultAsync(l => l.Id == liquidacionId);
        int? solId = liq?.SolicitudOperacionId;

        var docs = await _db.DocumentosAdjuntos
            .Include(d => d.SubidoPorUsuario)
            .Where(d => d.LiquidacionId == liquidacionId || (solId.HasValue && d.SolicitudOperacionId == solId.Value))
            .OrderByDescending(d => d.FechaSubida)
            .ToListAsync();

        return Ok(docs.Select(d => new DocumentoAdjuntoDto
        {
            Id = d.Id,
            TipoDocumento = d.TipoDocumento,
            NombreArchivo = d.NombreArchivo,
            RutaUrl = d.RutaUrl,
            SolicitudOperacionId = d.SolicitudOperacionId ?? solId,
            LiquidacionId = d.LiquidacionId ?? liquidacionId,
            ConductorId = d.ConductorId,
            SubidoPorUsuarioId = d.SubidoPorUsuarioId,
            SubidoPorNombre = d.SubidoPorUsuario?.NombreCompleto ?? "Usuario",
            FechaSubida = d.FechaSubida
        }).ToList());
    }

    [HttpGet("solicitud/{solicitudId}")]
    public async Task<ActionResult<List<DocumentoAdjuntoDto>>> GetPorSolicitud(int solicitudId)
    {
        var liq = await _db.Liquidaciones.FirstOrDefaultAsync(l => l.SolicitudOperacionId == solicitudId);
        int? liqId = liq?.Id;

        var docs = await _db.DocumentosAdjuntos
            .Include(d => d.SubidoPorUsuario)
            .Where(d => d.SolicitudOperacionId == solicitudId || (liqId.HasValue && d.LiquidacionId == liqId.Value))
            .OrderByDescending(d => d.FechaSubida)
            .ToListAsync();

        return Ok(docs.Select(d => new DocumentoAdjuntoDto
        {
            Id = d.Id,
            TipoDocumento = d.TipoDocumento,
            NombreArchivo = d.NombreArchivo,
            RutaUrl = d.RutaUrl,
            SolicitudOperacionId = d.SolicitudOperacionId ?? solicitudId,
            LiquidacionId = d.LiquidacionId ?? liqId,
            ConductorId = d.ConductorId,
            SubidoPorUsuarioId = d.SubidoPorUsuarioId,
            SubidoPorNombre = d.SubidoPorUsuario?.NombreCompleto ?? "Usuario",
            FechaSubida = d.FechaSubida
        }).ToList());
    }

    [HttpGet("conductor/{conductorId}")]
    [Authorize]
    public async Task<ActionResult<List<DocumentoAdjuntoDto>>> GetPorConductor(int conductorId)
    {
        var docs = await _db.DocumentosAdjuntos
            .Include(d => d.SubidoPorUsuario)
            .Where(d => d.ConductorId == conductorId)
            .OrderByDescending(d => d.FechaSubida)
            .ToListAsync();

        return Ok(docs.Select(d => new DocumentoAdjuntoDto
        {
            Id = d.Id,
            TipoDocumento = d.TipoDocumento,
            NombreArchivo = d.NombreArchivo,
            RutaUrl = d.RutaUrl,
            SolicitudOperacionId = d.SolicitudOperacionId,
            LiquidacionId = d.LiquidacionId,
            ConductorId = d.ConductorId,
            SubidoPorUsuarioId = d.SubidoPorUsuarioId,
            SubidoPorNombre = d.SubidoPorUsuario?.NombreCompleto ?? "Usuario",
            FechaSubida = d.FechaSubida
        }).ToList());
    }
}
