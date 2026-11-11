namespace LCP.Web.Models;

using LCP.Shared.DTOs;
using LCP.Shared.Enums;

public class DarsenaInventarioItemViewModel
{
    public int DarsenaId { get; set; }
    public string DarsenaCodigo { get; set; } = string.Empty;
    public int ContratoId { get; set; }
    public string ClienteRazonSocial { get; set; } = string.Empty;
    public PlanAlquilerDarsena Plan { get; set; }
    public string PlanNombre => Plan.ToString();
    public DateTime FechaInicio { get; set; }
    public DateTime FechaFin { get; set; }
    public bool EstaVigente { get; set; }
    public EstadoContratoDarsena EstadoContrato { get; set; }

    public int CapacidadPallets { get; set; } = 30;
    public decimal CapacidadM3 { get; set; } = 120m;

    public List<LoteInventarioDto> Lotes { get; set; } = new();

    public int TotalLotes => Lotes.Count;
    public int TotalBultosCajas => Lotes.Sum(l => l.CantidadBultosCajas);
    public int TotalUnidades => Lotes.Sum(l => l.CantidadUnidades);
    public decimal TotalPesoKg => Lotes.Sum(l => l.PesoKg);
    public decimal TotalVolumenM3 => Lotes.Sum(l => l.VolumenM3);

    public decimal PorcentajeOcupacionVolumen => CapacidadM3 > 0
        ? Math.Min(100, Math.Round((TotalVolumenM3 / CapacidadM3) * 100, 1))
        : 0;
}

public class MiStockViewModel
{
    public List<DarsenaInventarioItemViewModel> Darsenas { get; set; } = new();
    public int? DarsenaSeleccionadaId { get; set; }

    public int TotalDarsenasAlquiladas => Darsenas.Count;
    public int TotalLotesGlobal => Darsenas.Sum(d => d.TotalLotes);
    public int TotalBultosGlobal => Darsenas.Sum(d => d.TotalBultosCajas);
    public int TotalUnidadesGlobal => Darsenas.Sum(d => d.TotalUnidades);
    public decimal TotalPesoKgGlobal => Darsenas.Sum(d => d.TotalPesoKg);
    public decimal TotalVolumenM3Global => Darsenas.Sum(d => d.TotalVolumenM3);
}
