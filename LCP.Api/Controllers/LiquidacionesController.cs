using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using LCP.Api.Data;
using LCP.Shared.Constants;
using LCP.Shared.DTOs;
using LCP.Shared.Enums;

namespace LCP.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class LiquidacionesController : ControllerBase
{
    private readonly LcpDbContext _db;

    public LiquidacionesController(LcpDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<ActionResult<List<LiquidacionDto>>> GetLiquidaciones([FromQuery] EstadoLiquidacion? estado)
    {
        var query = _db.Liquidaciones
            .Include(l => l.ClienteEmpresa)
            .Include(l => l.AuditadoPorUsuario)
            .Include(l => l.Documentos)
                .ThenInclude(d => d.SubidoPorUsuario)
            .Include(l => l.SolicitudOperacion)
                .ThenInclude(s => s!.ConductorAsignado)
            .Include(l => l.SolicitudOperacion)
                .ThenInclude(s => s!.Darsena)
            .Include(l => l.SolicitudOperacion)
                .ThenInclude(s => s!.Documentos)
                    .ThenInclude(d => d.SubidoPorUsuario)
            .Include(l => l.ContratoAlquilerDarsena)
                .ThenInclude(c => c!.Darsena)
            .AsQueryable();

        // RBAC: Cliente solo ve sus liquidaciones
        if (User.IsInRole(Roles.Cliente))
        {
            var clienteIdStr = User.FindFirstValue("ClienteId");
            if (int.TryParse(clienteIdStr, out int clienteId))
            {
                query = query.Where(l => l.ClienteEmpresaId == clienteId);
            }
            else
            {
                return Ok(new List<LiquidacionDto>());
            }
        }

        if (estado.HasValue)
        {
            query = query.Where(l => l.Estado == estado.Value);
        }

        var list = await query.OrderByDescending(l => l.Id).ToListAsync();

        var dtos = list.Select(l => {
            // Combinar documentos de la liquidación y de la operación asociada sin duplicar
            var todosDocs = new List<DocumentoAdjuntoDto>();
            if (l.Documentos != null)
            {
                todosDocs.AddRange(l.Documentos.Select(d => new DocumentoAdjuntoDto
                {
                    Id = d.Id,
                    TipoDocumento = d.TipoDocumento,
                    NombreArchivo = d.NombreArchivo,
                    RutaUrl = d.RutaUrl,
                    LiquidacionId = d.LiquidacionId,
                    SolicitudOperacionId = d.SolicitudOperacionId,
                    ConductorId = d.ConductorId,
                    SubidoPorUsuarioId = d.SubidoPorUsuarioId,
                    SubidoPorNombre = d.SubidoPorUsuario?.NombreCompleto ?? "Usuario",
                    FechaSubida = d.FechaSubida
                }));
            }
            if (l.SolicitudOperacion?.Documentos != null)
            {
                foreach (var d in l.SolicitudOperacion.Documentos)
                {
                    if (!todosDocs.Any(x => x.Id == d.Id))
                    {
                        todosDocs.Add(new DocumentoAdjuntoDto
                        {
                            Id = d.Id,
                            TipoDocumento = d.TipoDocumento,
                            NombreArchivo = d.NombreArchivo,
                            RutaUrl = d.RutaUrl,
                            LiquidacionId = l.Id,
                            SolicitudOperacionId = d.SolicitudOperacionId,
                            ConductorId = d.ConductorId,
                            SubidoPorUsuarioId = d.SubidoPorUsuarioId,
                            SubidoPorNombre = d.SubidoPorUsuario?.NombreCompleto ?? "Usuario",
                            FechaSubida = d.FechaSubida
                        });
                    }
                }
            }

            var darsenaCod = l.ContratoAlquilerDarsena?.Darsena?.Codigo ?? l.SolicitudOperacion?.Darsena?.Codigo;

            return new LiquidacionDto
            {
                Id = l.Id,
                NumeroComprobante = l.NumeroComprobante,
                ClienteEmpresaId = l.ClienteEmpresaId,
                ClienteRazonSocial = l.ClienteEmpresa?.RazonSocial ?? "N/A",
                ClienteCUIT = l.ClienteEmpresa?.CUIT,
                ClienteEmail = l.ClienteEmpresa?.EmailContacto,
                ClienteTelefono = l.ClienteEmpresa?.TelefonoContacto,
                TipoLiquidacion = l.TipoLiquidacion,
                ContratoAlquilerDarsenaId = l.ContratoAlquilerDarsenaId,
                DarsenaCodigo = darsenaCod,
                SolicitudOperacionId = l.SolicitudOperacionId,
                CodigoSolicitud = l.SolicitudOperacion?.CodigoSolicitud,
                ModalidadTransporte = l.SolicitudOperacion?.ModalidadTransporte.ToString(),
                TipoOperacion = l.SolicitudOperacion?.TipoOperacion.ToString(),
                DescripcionMercaderia = l.SolicitudOperacion?.DescripcionMercaderia,
                PesoTotalKg = l.SolicitudOperacion?.PesoTotalKg,
                VolumenTotalM3 = l.SolicitudOperacion?.VolumenTotalM3,
                CiudadOrigen = l.SolicitudOperacion?.CiudadOrigen,
                CiudadDestino = l.SolicitudOperacion?.CiudadDestino,
                ConductorNombre = l.SolicitudOperacion?.ConductorAsignado?.NombreCompleto,
                ConductorPatente = l.SolicitudOperacion?.ConductorAsignado?.PatenteCamion,
                VariablesAplicadasResumen = l.VariablesAplicadasResumen,
                SubtotalPeso = l.SubtotalPeso,
                SubtotalVolumen = l.SubtotalVolumen,
                SubtotalDistancia = l.SubtotalDistancia,
                SubtotalTiempoDepositoOAlquiler = l.SubtotalTiempoDepositoOAlquiler,
                ImporteTotal = l.ImporteTotal,
                Estado = l.Estado,
                AuditadoPorUsuarioId = l.AuditadoPorUsuarioId,
                AuditadoPorNombre = l.AuditadoPorUsuario?.NombreCompleto,
                FechaAuditoria = l.FechaAuditoria,
                ObservacionAuditoria = l.ObservacionAuditoria,
                FechaEmision = l.FechaEmision,
                DocumentosRespaldatorios = todosDocs
            };
        }).ToList();

        return Ok(dtos);
    }

    [HttpGet("kpis")]
    [Authorize(Roles = "Auditor,Administrador")]
    public async Task<ActionResult<KpisLiquidacionDto>> GetKpis()
    {
        var pendientes = await _db.Liquidaciones
            .Where(l => l.Estado == EstadoLiquidacion.PendienteDeAuditoria)
            .ToListAsync();

        var primerDiaMes = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1);
        var validadasEsteMes = await _db.Liquidaciones
            .Where(l => l.Estado == EstadoLiquidacion.Validada && l.FechaAuditoria >= primerDiaMes)
            .ToListAsync();

        var totalValidadas = await _db.Liquidaciones
            .Where(l => l.Estado == EstadoLiquidacion.Validada)
            .ToListAsync();

        return Ok(new KpisLiquidacionDto
        {
            MontoPendienteAuditoria = pendientes.Sum(p => p.ImporteTotal),
            CantidadPendientes = pendientes.Count,
            MontoValidadoEsteMes = validadasEsteMes.Sum(v => v.ImporteTotal),
            CantidadValidadasEsteMes = validadasEsteMes.Count,
            MontoTotalLiquidado = totalValidadas.Sum(t => t.ImporteTotal),
            CantidadTotalValidadas = totalValidadas.Count
        });
    }

    [HttpPut("{id}/auditar")]
    [Authorize(Roles = "Auditor,Administrador")]
    public async Task<ActionResult> AuditarLiquidacion(int id, [FromBody] AuditarLiquidacionDto dto)
    {
        var liquidacion = await _db.Liquidaciones
            .Include(l => l.Documentos)
            .Include(l => l.SolicitudOperacion)
                .ThenInclude(s => s!.Documentos)
            .FirstOrDefaultAsync(l => l.Id == id);

        if (liquidacion == null) return NotFound(new { Mensaje = "Liquidación no encontrada." });

        // RESTRICCIÓN: Para poder confirmar/validar la liquidación, se debe haber cargado obligatoriamente la factura o comprobante
        if (dto.NuevoEstado == EstadoLiquidacion.Validada)
        {
            var tieneFacturaCargada = (liquidacion.Documentos != null && liquidacion.Documentos.Any()) ||
                                      (liquidacion.SolicitudOperacion?.Documentos != null && liquidacion.SolicitudOperacion.Documentos.Any());

            if (!tieneFacturaCargada)
            {
                return BadRequest(new { Mensaje = "No se puede confirmar ni validar la liquidación porque aún no se ha adjuntado ninguna factura comercial o remito respaldatorio. Los actores relacionados con la operación (el Cliente y/o el Chofer) deben cargar el comprobante antes de que Auditoría pueda validarla." });
            }
        }

        var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        int.TryParse(userIdStr, out int auditorId);

        liquidacion.Estado = dto.NuevoEstado;
        liquidacion.AuditadoPorUsuarioId = auditorId > 0 ? auditorId : null;
        liquidacion.FechaAuditoria = DateTime.UtcNow;
        liquidacion.ObservacionAuditoria = dto.Observacion;

        await _db.SaveChangesAsync();

        return Ok(new { Mensaje = $"Liquidación {liquidacion.NumeroComprobante} validada exitosamente." });
    }
}
