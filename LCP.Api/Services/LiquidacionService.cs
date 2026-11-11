using Microsoft.EntityFrameworkCore;
using LCP.Api.Data;
using LCP.Api.Data.Entities;
using LCP.Shared.Constants;
using LCP.Shared.Enums;

namespace LCP.Api.Services;

public interface ILiquidacionService
{
    Task<Liquidacion> GenerarLiquidacionPorContratoDarsenaAsync(ContratoAlquilerDarsena contrato);
    Task<Liquidacion> GenerarLiquidacionPorSolicitudCompletadaAsync(int solicitudId);
}

public class LiquidacionService : ILiquidacionService
{
    private readonly LcpDbContext _db;

    public LiquidacionService(LcpDbContext db) => _db = db;

    public async Task<Liquidacion> GenerarLiquidacionPorContratoDarsenaAsync(ContratoAlquilerDarsena contrato)
    {
        string codigoTarifa = contrato.Plan switch
        {
            PlanAlquilerDarsena.Trimestral => TarifaCodigos.AlquilerTrimestral,
            PlanAlquilerDarsena.Semestral  => TarifaCodigos.AlquilerSemestral,
            PlanAlquilerDarsena.Anual      => TarifaCodigos.AlquilerAnual,
            _ => throw new InvalidOperationException("Plan de alquiler inválido.")
        };

        var tarifa = await _db.TarifasConfiguracion
            .FirstOrDefaultAsync(t => t.CodigoConcepto == codigoTarifa);

        decimal valorPlan = tarifa?.ValorUnitario ?? (contrato.Plan switch
        {
            PlanAlquilerDarsena.Trimestral => 450000m,
            PlanAlquilerDarsena.Semestral => 820000m,
            PlanAlquilerDarsena.Anual => 1500000m,
            _ => 450000m
        });

        var liquidacion = new Liquidacion
        {
            NumeroComprobante = await GenerarSiguienteComprobanteAsync(),
            ClienteEmpresaId = contrato.ClienteEmpresaId,
            TipoLiquidacion = TipoLiquidacion.CanonAlquilerDarsena,
            ContratoAlquilerDarsenaId = contrato.Id,
            VariablesAplicadasResumen = $"Alquiler {contrato.Plan} - Dársena #{contrato.DarsenaId}",
            SubtotalTiempoDepositoOAlquiler = valorPlan,
            ImporteTotal = valorPlan,
            Estado = EstadoLiquidacion.PendienteDeAuditoria, // Regla de negocio: SIEMPRE nace pendiente de auditoría
            FechaEmision = DateTime.UtcNow
        };

        _db.Liquidaciones.Add(liquidacion);
        await _db.SaveChangesAsync();
        return liquidacion;
    }

    public async Task<Liquidacion> GenerarLiquidacionPorSolicitudCompletadaAsync(int solicitudId)
    {
        var existente = await _db.Liquidaciones.FirstOrDefaultAsync(l => l.SolicitudOperacionId == solicitudId);
        if (existente != null)
        {
            return existente;
        }

        var solicitud = await _db.SolicitudesOperacion
            .Include(s => s.ClienteEmpresa)
            .FirstOrDefaultAsync(s => s.Id == solicitudId)
            ?? throw new KeyNotFoundException($"Solicitud #{solicitudId} no encontrada.");

        var tarifas = await _db.TarifasConfiguracion
            .ToDictionaryAsync(t => t.CodigoConcepto, t => t.ValorUnitario);

        decimal subPeso = 0, subVolumen = 0, subDistancia = 0, subOperativo = 0;
        var variablesUsadas = new List<string>();

        // Cargo operativo de descarga/carga por el Operador en la dársena alquilada
        subOperativo = tarifas.GetValueOrDefault(TarifaCodigos.CargoDescargaOperador, 15000m);
        variablesUsadas.Add("Operativa de dársena");

        // Si el cliente solicitó Flota de LCP, se liquida Peso + Volumen + Distancia
        if (solicitud.ModalidadTransporte == ModalidadTransporte.FlotaLCP)
        {
            subPeso = solicitud.PesoTotalKg * tarifas.GetValueOrDefault(TarifaCodigos.PrecioPorKg, 50m);
            subVolumen = solicitud.VolumenTotalM3 * tarifas.GetValueOrDefault(TarifaCodigos.PrecioPorM3, 8000m);
            subDistancia = solicitud.DistanciaEstimadaKm * tarifas.GetValueOrDefault(TarifaCodigos.PrecioPorKm, 1200m);
            variablesUsadas.Add("Peso, volumen y distancia (Flota LCP)");
        }
        else
        {
            variablesUsadas.Add("Chofer propio (solo muelle)");
        }

        var liquidacion = new Liquidacion
        {
            NumeroComprobante = await GenerarSiguienteComprobanteAsync(),
            ClienteEmpresaId = solicitud.ClienteEmpresaId,
            TipoLiquidacion = TipoLiquidacion.ServicioTransporteYPatio,
            SolicitudOperacionId = solicitud.Id,
            VariablesAplicadasResumen = string.Join(" + ", variablesUsadas),
            SubtotalPeso = subPeso,
            SubtotalVolumen = subVolumen,
            SubtotalDistancia = subDistancia,
            SubtotalTiempoDepositoOAlquiler = subOperativo,
            ImporteTotal = subPeso + subVolumen + subDistancia + subOperativo,
            Estado = EstadoLiquidacion.PendienteDeAuditoria, // Nace en pendiente de auditoría obligatoria
            FechaEmision = DateTime.UtcNow
        };

        _db.Liquidaciones.Add(liquidacion);
        await _db.SaveChangesAsync();
        return liquidacion;
    }

    private async Task<string> GenerarSiguienteComprobanteAsync()
    {
        var ultimoId = await _db.Liquidaciones
            .OrderByDescending(l => l.Id)
            .Select(l => l.Id)
            .FirstOrDefaultAsync();

        int proximoNumero = ultimoId + 43; // Formato 0001-XXXX
        return $"0001-{proximoNumero:D4}";
    }
}
