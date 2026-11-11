using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using LCP.Api.Data;
using LCP.Api.Data.Entities;
using LCP.Api.Hubs;
using LCP.Api.Services;
using LCP.Shared.Constants;
using LCP.Shared.DTOs;
using LCP.Shared.Enums;

namespace LCP.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ContratosController : ControllerBase
{
    private readonly LcpDbContext _db;
    private readonly ILiquidacionService _liquidacionService;
    private readonly IFileStorageService _fileStorage;
    private readonly IHubContext<PatioHub> _patioHub;

    public ContratosController(LcpDbContext db, ILiquidacionService liquidacionService, IFileStorageService fileStorage, IHubContext<PatioHub> patioHub)
    {
        _db = db;
        _liquidacionService = liquidacionService;
        _fileStorage = fileStorage;
        _patioHub = patioHub;
    }

    [HttpGet]
    public async Task<ActionResult<List<ContratoAlquilerDarsenaDto>>> GetContratos()
    {
        var query = _db.ContratosAlquilerDarsena
            .Include(c => c.ClienteEmpresa)
            .Include(c => c.Darsena)
            .AsQueryable();

        // RBAC: Cliente solo ve sus propios contratos
        if (User.IsInRole(Roles.Cliente))
        {
            var clienteIdStr = User.FindFirstValue("ClienteId");
            if (int.TryParse(clienteIdStr, out int clienteId))
            {
                query = query.Where(c => c.ClienteEmpresaId == clienteId);
            }
            else
            {
                return Ok(new List<ContratoAlquilerDarsenaDto>());
            }
        }

        var list = await query.OrderByDescending(c => c.Id).ToListAsync();

        var darsenasIds = list.Select(c => c.DarsenaId).Distinct().ToList();
        var ocupaciones = await _db.LotesInventario
            .Where(l => darsenasIds.Contains(l.DarsenaId))
            .GroupBy(l => l.DarsenaId)
            .Select(g => new { DarsenaId = g.Key, VolumenOcupado = g.Sum(l => l.VolumenM3) })
            .ToDictionaryAsync(x => x.DarsenaId, x => x.VolumenOcupado);

        var dtos = list.Select(c =>
        {
            decimal ocupado = ocupaciones.GetValueOrDefault(c.DarsenaId, 0m);
            return new ContratoAlquilerDarsenaDto
            {
                Id = c.Id,
                ClienteEmpresaId = c.ClienteEmpresaId,
                ClienteRazonSocial = c.ClienteEmpresa?.RazonSocial ?? "N/A",
                DarsenaId = c.DarsenaId,
                DarsenaCodigo = c.Darsena?.Codigo ?? $"Dársena #{c.DarsenaId}",
                Plan = c.Plan,
                FechaInicio = c.FechaInicio,
                FechaFin = c.FechaFin,
                MontoTotalContrato = c.MontoTotalContrato,
                Estado = c.Estado,
                CapacidadMaximaM3 = c.Darsena?.CapacidadMaximaM3 ?? 120m,
                CapacidadMaximaPallets = c.Darsena?.CapacidadMaximaPallets ?? 30,
                VolumenOcupadoM3 = ocupado,
                MercaderiaAlmacenar = c.MercaderiaAlmacenar ?? "Carga General",
                TipoAlmacenamiento = c.TipoAlmacenamiento ?? "Ambiente",
                ConsideracionesEspeciales = c.ConsideracionesEspeciales,
                ObservacionAuditor = c.ObservacionAuditor,
                ComprobantePagoUrl = c.ComprobantePagoUrl,
                FechaAprobacionAuditor = c.FechaAprobacionAuditor,
                FechaPagoFactura = c.FechaPagoFactura
            };
        }).ToList();

        return Ok(dtos);
    }

    [HttpPost]
    [Authorize(Roles = "Cliente,Administrador")]
    public async Task<ActionResult<ContratoAlquilerDarsenaDto>> CrearContrato([FromBody] CrearContratoDarsenaDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        int clienteId = dto.ClienteEmpresaId;

        // Si es cliente, validar que no cree para otro
        if (User.IsInRole(Roles.Cliente))
        {
            var claimClienteId = User.FindFirstValue("ClienteId");
            if (int.TryParse(claimClienteId, out int idPropio))
            {
                clienteId = idPropio;
            }
        }

        var darsena = await _db.Darsenas.FindAsync(dto.DarsenaId);
        if (darsena == null) return NotFound(new { Mensaje = "Dársena no encontrada." });

        // Verificar si la dársena ya está alquilada activamente por otro cliente
        var contratoActivoExistente = await _db.ContratosAlquilerDarsena
            .AnyAsync(c => c.DarsenaId == dto.DarsenaId &&
                           c.Estado == EstadoContratoDarsena.Activo &&
                           c.FechaFin >= DateTime.UtcNow &&
                           c.ClienteEmpresaId != clienteId);

        if (contratoActivoExistente)
        {
            return BadRequest(new { Mensaje = "La dársena seleccionada ya se encuentra alquilada por otra empresa." });
        }

        // Determinar duración y monto según tarifas
        int meses = dto.Plan switch
        {
            PlanAlquilerDarsena.Trimestral => 3,
            PlanAlquilerDarsena.Semestral => 6,
            PlanAlquilerDarsena.Anual => 12,
            _ => 3
        };

        string codigoTarifa = dto.Plan switch
        {
            PlanAlquilerDarsena.Trimestral => TarifaCodigos.AlquilerTrimestral,
            PlanAlquilerDarsena.Semestral => TarifaCodigos.AlquilerSemestral,
            PlanAlquilerDarsena.Anual => TarifaCodigos.AlquilerAnual,
            _ => TarifaCodigos.AlquilerTrimestral
        };

        var tarifa = await _db.TarifasConfiguracion.FirstOrDefaultAsync(t => t.CodigoConcepto == codigoTarifa);
        decimal monto = tarifa?.ValorUnitario ?? 450000m;

        // Se crea el contrato en estado PendienteRevision para evaluación del Auditor
        var contrato = new ContratoAlquilerDarsena
        {
            ClienteEmpresaId = clienteId,
            DarsenaId = dto.DarsenaId,
            Plan = dto.Plan,
            FechaInicio = DateTime.UtcNow,
            FechaFin = DateTime.UtcNow.AddMonths(meses),
            MontoTotalContrato = monto,
            Estado = EstadoContratoDarsena.PendienteRevision,
            MercaderiaAlmacenar = dto.MercaderiaAlmacenar,
            TipoAlmacenamiento = dto.TipoAlmacenamiento,
            ConsideracionesEspeciales = dto.ConsideracionesEspeciales
        };

        _db.ContratosAlquilerDarsena.Add(contrato);
        await _db.SaveChangesAsync();

        var cliente = await _db.ClientesEmpresa.FindAsync(clienteId);

        return Ok(new ContratoAlquilerDarsenaDto
        {
            Id = contrato.Id,
            ClienteEmpresaId = contrato.ClienteEmpresaId,
            ClienteRazonSocial = cliente?.RazonSocial ?? "N/A",
            DarsenaId = contrato.DarsenaId,
            DarsenaCodigo = darsena.Codigo,
            Plan = contrato.Plan,
            FechaInicio = contrato.FechaInicio,
            FechaFin = contrato.FechaFin,
            MontoTotalContrato = contrato.MontoTotalContrato,
            Estado = contrato.Estado,
            MercaderiaAlmacenar = contrato.MercaderiaAlmacenar,
            TipoAlmacenamiento = contrato.TipoAlmacenamiento,
            ConsideracionesEspeciales = contrato.ConsideracionesEspeciales
        });
    }

    [HttpPut("{id}/aprobar-auditor")]
    [Authorize(Roles = "Auditor,Administrador")]
    public async Task<ActionResult> AprobarSolicitudAuditor(int id, [FromBody] AprobarSolicitudDarsenaDto? dto)
    {
        var contrato = await _db.ContratosAlquilerDarsena
            .Include(c => c.ClienteEmpresa)
            .Include(c => c.Darsena)
            .FirstOrDefaultAsync(c => c.Id == id);

        if (contrato == null) return NotFound(new { Mensaje = "Contrato de dársena no encontrado." });

        if (contrato.Estado != EstadoContratoDarsena.PendienteRevision)
        {
            return BadRequest(new { Mensaje = $"El contrato no se encuentra en estado Pendiente de Revisión (Estado actual: {contrato.Estado})." });
        }

        contrato.Estado = EstadoContratoDarsena.AprobadoPendientePago;
        contrato.FechaAprobacionAuditor = DateTime.UtcNow;
        contrato.ObservacionAuditor = dto?.ObservacionAuditor ?? "Solicitud técnica aprobada por el Auditor. Se requiere pago y carga de factura.";

        // Generar la liquidación para que el cliente tenga el comprobante/liquidación oficial
        await _liquidacionService.GenerarLiquidacionPorContratoDarsenaAsync(contrato);

        await _db.SaveChangesAsync();

        return Ok(new { Mensaje = "Solicitud de dársena aprobada. Se ha habilitado la etapa de facturación y pago para el cliente." });
    }

    [HttpPut("{id}/rechazar-auditor")]
    [Authorize(Roles = "Auditor,Administrador")]
    public async Task<ActionResult> RechazarSolicitudAuditor(int id, [FromBody] RechazarSolicitudDarsenaDto dto)
    {
        var contrato = await _db.ContratosAlquilerDarsena.FindAsync(id);
        if (contrato == null) return NotFound(new { Mensaje = "Contrato de dársena no encontrado." });

        contrato.Estado = EstadoContratoDarsena.Rechazado;
        contrato.ObservacionAuditor = dto.Motivo;

        await _db.SaveChangesAsync();

        return Ok(new { Mensaje = "Solicitud de dársena rechazada." });
    }

    [HttpPost("{id}/cargar-factura")]
    [Authorize(Roles = "Cliente,Administrador")]
    public async Task<ActionResult> CargarFacturaPago(int id, [FromForm] IFormFile archivo)
    {
        if (archivo == null || archivo.Length == 0)
        {
            return BadRequest(new { Mensaje = "Debe adjuntar el archivo de la factura o comprobante de pago." });
        }

        var contrato = await _db.ContratosAlquilerDarsena
            .Include(c => c.ClienteEmpresa)
            .Include(c => c.Darsena)
            .FirstOrDefaultAsync(c => c.Id == id);

        if (contrato == null) return NotFound(new { Mensaje = "Contrato no encontrado." });

        // Validar propiedad si es cliente
        if (User.IsInRole(Roles.Cliente))
        {
            var claimClienteId = User.FindFirstValue("ClienteId");
            if (int.TryParse(claimClienteId, out int idPropio) && contrato.ClienteEmpresaId != idPropio)
            {
                return Forbid();
            }
        }

        if (contrato.Estado != EstadoContratoDarsena.AprobadoPendientePago && contrato.Estado != EstadoContratoDarsena.PendienteRevision)
        {
            return BadRequest(new { Mensaje = "El contrato ya ha sido activado o no está en estado pendiente de pago." });
        }

        using var stream = archivo.OpenReadStream();
        var rutaUrl = await _fileStorage.GuardarArchivoAsync(stream, archivo.FileName, "facturas");

        contrato.ComprobantePagoUrl = rutaUrl;
        contrato.FechaPagoFactura = DateTime.UtcNow;
        contrato.Estado = EstadoContratoDarsena.FacturaCargadaPendienteHabilitacion;

        // Registrar en DocumentosAdjuntos vinculado a la liquidación correspondiente si existe
        int subidoPorId = 1;
        var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (int.TryParse(userIdStr, out int uid)) subidoPorId = uid;

        var liquidacion = await _db.Liquidaciones
            .FirstOrDefaultAsync(l => l.ClienteEmpresaId == contrato.ClienteEmpresaId &&
                                      l.TipoLiquidacion == TipoLiquidacion.CanonAlquilerDarsena &&
                                      l.Estado == EstadoLiquidacion.PendienteDeAuditoria);

        var doc = new DocumentoAdjunto
        {
            TipoDocumento = "FacturaPagoDarsena",
            NombreArchivo = archivo.FileName,
            RutaUrl = rutaUrl,
            FechaSubida = DateTime.UtcNow,
            SubidoPorUsuarioId = subidoPorId,
            LiquidacionId = liquidacion?.Id
        };
        _db.DocumentosAdjuntos.Add(doc);

        await _db.SaveChangesAsync();

        return Ok(new { Mensaje = "Factura o comprobante de pago cargado exitosamente. El Auditor revisará el comprobante para habilitar formalmente la dársena.", RutaUrl = rutaUrl });
    }

    [HttpPut("{id}/habilitar-darsena")]
    [Authorize(Roles = "Auditor,Administrador")]
    public async Task<ActionResult> HabilitarDarsenaAuditor(int id)
    {
        var contrato = await _db.ContratosAlquilerDarsena
            .Include(c => c.ClienteEmpresa)
            .Include(c => c.Darsena)
            .FirstOrDefaultAsync(c => c.Id == id);

        if (contrato == null) return NotFound(new { Mensaje = "Contrato de dársena no encontrado." });

        int meses = contrato.Plan switch
        {
            PlanAlquilerDarsena.Trimestral => 3,
            PlanAlquilerDarsena.Semestral => 6,
            PlanAlquilerDarsena.Anual => 12,
            _ => 3
        };

        contrato.Estado = EstadoContratoDarsena.Activo;
        contrato.FechaInicio = DateTime.UtcNow;
        contrato.FechaFin = DateTime.UtcNow.AddMonths(meses);

        // Se asigna y habilita formalmente la dársena para el cliente
        if (contrato.Darsena != null)
        {
            contrato.Darsena.ClienteAlquilerActualId = contrato.ClienteEmpresaId;
            contrato.Darsena.EstadoActual = EstadoDarsena.Libre;
            contrato.Darsena.DetalleOperacionActual = $"Alquilada por {contrato.ClienteEmpresa?.RazonSocial} ({contrato.TipoAlmacenamiento ?? "Ambiente"}) · Muelle listo para operar";
        }

        // Marcar liquidación asociada como Validada si existe
        var liquidacion = await _db.Liquidaciones
            .FirstOrDefaultAsync(l => l.ClienteEmpresaId == contrato.ClienteEmpresaId &&
                                      l.TipoLiquidacion == TipoLiquidacion.CanonAlquilerDarsena &&
                                      l.Estado == EstadoLiquidacion.PendienteDeAuditoria);
        if (liquidacion != null)
        {
            liquidacion.Estado = EstadoLiquidacion.Validada;
            liquidacion.FechaAuditoria = DateTime.UtcNow;
            liquidacion.ObservacionAuditoria = "Factura de alquiler verificada y dársena habilitada.";
        }

        await _db.SaveChangesAsync();

        if (contrato.Darsena != null)
        {
            await _patioHub.Clients.All.SendAsync("DarsenaActualizada", new
            {
                DarsenaId = contrato.DarsenaId,
                NuevoEstado = EstadoDarsena.Libre,
                ClienteRazonSocial = contrato.ClienteEmpresa?.RazonSocial,
                Detalle = contrato.Darsena?.DetalleOperacionActual
            });
        }

        return Ok(new { Mensaje = $"Dársena #{contrato.Darsena?.Codigo} habilitada formalmente para {contrato.ClienteEmpresa?.RazonSocial}. Ya puede operar y cargar stock." });
    }
}
