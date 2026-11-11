using System.ComponentModel.DataAnnotations;
using LCP.Shared.Enums;

namespace LCP.Shared.DTOs;

public class LiquidacionDto
{
    public int Id { get; set; }
    public string NumeroComprobante { get; set; } = string.Empty;
    public int ClienteEmpresaId { get; set; }
    public string ClienteRazonSocial { get; set; } = string.Empty;
    public string? ClienteCUIT { get; set; }
    public string? ClienteEmail { get; set; }
    public string? ClienteTelefono { get; set; }
    public TipoLiquidacion TipoLiquidacion { get; set; }
    public string TipoLiquidacionNombre => TipoLiquidacion == TipoLiquidacion.CanonAlquilerDarsena ? "Alquiler de Dársena" : "Servicio Transporte y Patio";
    public int? ContratoAlquilerDarsenaId { get; set; }
    public string? DarsenaCodigo { get; set; }
    public int? SolicitudOperacionId { get; set; }
    public string? CodigoSolicitud { get; set; }
    public string? ModalidadTransporte { get; set; }
    public string? TipoOperacion { get; set; }
    public string? DescripcionMercaderia { get; set; }
    public decimal? PesoTotalKg { get; set; }
    public decimal? VolumenTotalM3 { get; set; }
    public string? CiudadOrigen { get; set; }
    public string? CiudadDestino { get; set; }
    public string? ConductorNombre { get; set; }
    public string? ConductorPatente { get; set; }
    public string VariablesAplicadasResumen { get; set; } = string.Empty;
    public decimal SubtotalPeso { get; set; }
    public decimal SubtotalVolumen { get; set; }
    public decimal SubtotalDistancia { get; set; }
    public decimal SubtotalTiempoDepositoOAlquiler { get; set; }
    public decimal ImporteTotal { get; set; }
    public EstadoLiquidacion Estado { get; set; }
    public string EstadoNombre => Estado == EstadoLiquidacion.PendienteDeAuditoria ? "Pendiente de auditoría" :
                                   Estado == EstadoLiquidacion.Validada ? "Validada" : "Observada";
    public int? AuditadoPorUsuarioId { get; set; }
    public string? AuditadoPorNombre { get; set; }
    public DateTime? FechaAuditoria { get; set; }
    public string? ObservacionAuditoria { get; set; }
    public DateTime FechaEmision { get; set; }
    public List<DocumentoAdjuntoDto> DocumentosRespaldatorios { get; set; } = new();
}

public class AuditarLiquidacionDto
{
    [Required]
    public int LiquidacionId { get; set; }

    [Required]
    public EstadoLiquidacion NuevoEstado { get; set; } // Validada u Observada

    public string? Observacion { get; set; }
}

public class KpisLiquidacionDto
{
    public decimal MontoPendienteAuditoria { get; set; }
    public int CantidadPendientes { get; set; }
    public decimal MontoValidadoEsteMes { get; set; }
    public int CantidadValidadasEsteMes { get; set; }
    public decimal MontoTotalLiquidado { get; set; }
    public int CantidadTotalValidadas { get; set; }
}

public class TarifaConfiguracionDto
{
    public int Id { get; set; }
    public string CodigoConcepto { get; set; } = string.Empty;
    public string Descripcion { get; set; } = string.Empty;
    public decimal ValorUnitario { get; set; }
    public DateTime UltimaActualizacion { get; set; }
    public int ActualizadoPorUsuarioId { get; set; }
    public string? ActualizadoPorNombre { get; set; }
}

public class ActualizarTarifaDto
{
    [Required]
    public string CodigoConcepto { get; set; } = string.Empty;

    [Range(0, 100000000, ErrorMessage = "El valor debe ser positivo.")]
    public decimal NuevoValor { get; set; }
}
