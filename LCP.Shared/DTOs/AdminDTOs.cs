using LCP.Shared.Enums;

namespace LCP.Shared.DTOs;

public class ConductorDetalleDto
{
    public int Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string Apellido { get; set; } = string.Empty;
    public string NombreCompleto { get; set; } = string.Empty;
    public string CUIL { get; set; } = string.Empty;
    public DateTime FechaNacimiento { get; set; }
    public string CelularWhatsApp { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Domicilio { get; set; } = string.Empty;
    public string Localidad { get; set; } = string.Empty;
    public string Provincia { get; set; } = string.Empty;
    public string PatenteCamion { get; set; } = string.Empty;
    public decimal CapacidadCargaKg { get; set; }
    public decimal CapacidadVolumenM3 { get; set; }
    public TipoConductor TipoConductor { get; set; }
    public string TipoConductorNombre { get; set; } = string.Empty;
    public int? ClienteEmpresaId { get; set; }
    public string? ClienteRazonSocial { get; set; }
    public EstadoPostulacionAlta EstadoPostulacion { get; set; }
    public string EstadoNombre { get; set; } = string.Empty;
    public DateTime FechaPostulacion { get; set; }
    public int? UsuarioId { get; set; }
    public bool UsuarioActivo { get; set; } = true;
    public int DocumentosCount { get; set; }
    public string? LicenciaArchivoUrl { get; set; }
    public string? SeguroCamionArchivoUrl { get; set; }
    public bool EmailConfirmado { get; set; }
    public DateTime? FechaConfirmacionEmail { get; set; }
}

public class ClienteEmpresaDetalleDto
{
    public int Id { get; set; }
    public string RazonSocial { get; set; } = string.Empty;
    public string CUIT { get; set; } = string.Empty;
    public string Rubro { get; set; } = string.Empty;
    public string Domicilio { get; set; } = string.Empty;
    public string Localidad { get; set; } = string.Empty;
    public string Provincia { get; set; } = string.Empty;
    public string EmailContacto { get; set; } = string.Empty;
    public string TelefonoContacto { get; set; } = string.Empty;
    public EstadoPostulacionAlta EstadoAlta { get; set; }
    public string EstadoNombre { get; set; } = string.Empty;
    public bool EmailConfirmado { get; set; }
    public DateTime? FechaConfirmacionEmail { get; set; }
    public DateTime FechaSolicitud { get; set; }
}

