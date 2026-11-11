using System.ComponentModel.DataAnnotations;
using LCP.Shared.Enums;

namespace LCP.Shared.DTOs;

public class LoginRequestDto
{
    [Required(ErrorMessage = "El correo electrónico es requerido.")]
    [EmailAddress(ErrorMessage = "El correo no tiene un formato válido.")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "La contraseña es requerida.")]
    public string Password { get; set; } = string.Empty;

    public bool Recordarme { get; set; } = false;
}

public class LoginResponseDto
{
    public bool Exitoso { get; set; }
    public string Mensaje { get; set; } = string.Empty;
    public string Token { get; set; } = string.Empty;
    public DateTime Expiracion { get; set; }
    public UsuarioDto? Usuario { get; set; }
}

public class UsuarioDto
{
    public int Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string Apellido { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Telefono { get; set; } = string.Empty;
    public RolUsuario Rol { get; set; }
    public string RolNombre => Rol.ToString();
    public bool Activo { get; set; }
    public bool DebeCambiarPassword { get; set; }
    public int? ClienteId { get; set; }
    public string? EmpresaRazonSocial { get; set; }
    public DateTime FechaAlta { get; set; }
    public string NombreCompleto => $"{Nombre} {Apellido}".Trim();

    // Campos de RRHH y Legajo de Personal
    public string? Dni { get; set; }
    public DateTime? FechaNacimiento { get; set; }
    public string? Direccion { get; set; }
    public string? Localidad { get; set; }
    public string? Provincia { get; set; }
    public string? CodigoPostal { get; set; }
    public string? EstadoCivil { get; set; }
    public string? ContactoEmergenciaNombre { get; set; }
    public string? ContactoEmergenciaTelefono { get; set; }
    public string? PuestoCargo { get; set; }
    public DateTime? FechaIngreso { get; set; }
    public string? ModalidadContratacion { get; set; }
    public string? ObservacionesLegajo { get; set; }
    public string? DocumentoIdentidadUrl { get; set; }
    public string? CvArchivoUrl { get; set; }
    public bool EmailConfirmado { get; set; }
    public string? TokenConfirmacionEmail { get; set; }
    public DateTime? FechaConfirmacionEmail { get; set; }
}

public class CrearPersonalDto
{
    [Required(ErrorMessage = "El nombre es obligatorio.")]
    [MaxLength(100, ErrorMessage = "El nombre no puede superar los 100 caracteres.")]
    public string Nombre { get; set; } = string.Empty;

    [Required(ErrorMessage = "El apellido es obligatorio.")]
    [MaxLength(100, ErrorMessage = "El apellido no puede superar los 100 caracteres.")]
    public string Apellido { get; set; } = string.Empty;

    [Required(ErrorMessage = "El DNI / CUIL es obligatorio.")]
    [MaxLength(25, ErrorMessage = "El documento no puede superar los 25 caracteres.")]
    public string Dni { get; set; } = string.Empty;

    [Required(ErrorMessage = "La fecha de nacimiento es obligatoria.")]
    public DateTime FechaNacimiento { get; set; } = new DateTime(1995, 1, 1);

    [Required(ErrorMessage = "El correo electrónico corporativo es obligatorio.")]
    [EmailAddress(ErrorMessage = "Debe ingresar una dirección de correo válida.")]
    [MaxLength(150, ErrorMessage = "El correo no puede superar los 150 caracteres.")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "El teléfono / celular es obligatorio.")]
    [MaxLength(50, ErrorMessage = "El teléfono no puede superar los 50 caracteres.")]
    public string Telefono { get; set; } = string.Empty;

    [Required(ErrorMessage = "El domicilio es obligatorio.")]
    [MaxLength(200, ErrorMessage = "El domicilio no puede superar los 200 caracteres.")]
    public string Direccion { get; set; } = string.Empty;

    [Required(ErrorMessage = "La localidad es obligatoria.")]
    [MaxLength(100)]
    public string Localidad { get; set; } = "Santa Rosa";

    [Required(ErrorMessage = "La provincia es obligatoria.")]
    [MaxLength(100)]
    public string Provincia { get; set; } = "La Pampa";

    [MaxLength(20)]
    public string? CodigoPostal { get; set; } = "6300";

    [MaxLength(50)]
    public string? EstadoCivil { get; set; } = "Soltero/a";

    [Required(ErrorMessage = "El nombre del contacto de emergencia es obligatorio.")]
    [MaxLength(150)]
    public string ContactoEmergenciaNombre { get; set; } = string.Empty;

    [Required(ErrorMessage = "El teléfono del contacto de emergencia es obligatorio.")]
    [MaxLength(50)]
    public string ContactoEmergenciaTelefono { get; set; } = string.Empty;

    [Required(ErrorMessage = "Debe asignar un rol en el sistema.")]
    public RolUsuario Rol { get; set; } = RolUsuario.Operador;

    [Required(ErrorMessage = "El puesto o cargo a desempeñar es obligatorio.")]
    [MaxLength(100)]
    public string PuestoCargo { get; set; } = string.Empty;

    [Required(ErrorMessage = "La fecha de ingreso a la empresa es obligatoria.")]
    public DateTime FechaIngreso { get; set; } = DateTime.Today;

    [MaxLength(100)]
    public string? ModalidadContratacion { get; set; } = "Tiempo Indeterminado (Efectivo)";

    [MaxLength(500)]
    public string? ObservacionesLegajo { get; set; }

    public string? DocumentoIdentidadUrl { get; set; }
    public string? CvArchivoUrl { get; set; }
}

public class RegistroEmpresaDto
{
    [Required(ErrorMessage = "La razón social es obligatoria.")]
    public string RazonSocial { get; set; } = string.Empty;

    [Required(ErrorMessage = "El CUIT es obligatorio.")]
    public string CUIT { get; set; } = string.Empty;

    [Required(ErrorMessage = "El rubro es obligatorio.")]
    public string Rubro { get; set; } = string.Empty;

    public string Domicilio { get; set; } = string.Empty;
    public string Localidad { get; set; } = string.Empty;
    public string Provincia { get; set; } = string.Empty;

    [Required(ErrorMessage = "El correo de contacto es obligatorio.")]
    [EmailAddress]
    public string EmailContacto { get; set; } = string.Empty;

    [Required(ErrorMessage = "El teléfono de contacto es obligatorio.")]
    public string TelefonoContacto { get; set; } = string.Empty;
}

public class CambiarPasswordDto
{
    [Required(ErrorMessage = "La contraseña actual es obligatoria.")]
    public string PasswordActual { get; set; } = string.Empty;

    [Required(ErrorMessage = "La nueva contraseña es obligatoria.")]
    [MinLength(6, ErrorMessage = "La nueva contraseña debe tener al menos 6 caracteres.")]
    public string PasswordNuevo { get; set; } = string.Empty;

    [Required(ErrorMessage = "Debe confirmar la nueva contraseña.")]
    [Compare("PasswordNuevo", ErrorMessage = "Las contraseñas no coinciden.")]
    public string ConfirmarPassword { get; set; } = string.Empty;
}

public class PostulacionChoferDto
{
    [Required(ErrorMessage = "El nombre es obligatorio.")]
    public string Nombre { get; set; } = string.Empty;

    [Required(ErrorMessage = "El apellido es obligatorio.")]
    public string Apellido { get; set; } = string.Empty;

    [Required(ErrorMessage = "El CUIL es obligatorio.")]
    public string CUIL { get; set; } = string.Empty;

    [Required(ErrorMessage = "La fecha de nacimiento es obligatoria.")]
    public DateTime FechaNacimiento { get; set; }

    [Required(ErrorMessage = "El celular / WhatsApp es obligatorio.")]
    public string CelularWhatsApp { get; set; } = string.Empty;

    [Required(ErrorMessage = "El correo electrónico es obligatorio.")]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "El domicilio es obligatorio.")]
    public string Domicilio { get; set; } = string.Empty;

    [Required(ErrorMessage = "La localidad es obligatoria.")]
    public string Localidad { get; set; } = string.Empty;

    [Required(ErrorMessage = "La provincia es obligatoria.")]
    public string Provincia { get; set; } = string.Empty;

    public string PatenteCamion { get; set; } = string.Empty;
    public decimal CapacidadCargaKg { get; set; }
    public decimal CapacidadVolumenM3 { get; set; }

    public TipoConductor TipoConductor { get; set; } = TipoConductor.FlotaLCP;
    public int? ClienteEmpresaId { get; set; }

    [Range(typeof(bool), "true", "true", ErrorMessage = "Debe aceptar la declaración jurada para postularse.")]
    public bool DeclaracionJuradaAceptada { get; set; }

    public string? LicenciaArchivoUrl { get; set; }
    public string? SeguroCamionArchivoUrl { get; set; }
}

public class CrearConductorFlotaDto
{
    [Required(ErrorMessage = "El nombre es obligatorio.")]
    [MaxLength(100)]
    public string Nombre { get; set; } = string.Empty;

    [Required(ErrorMessage = "El apellido es obligatorio.")]
    [MaxLength(100)]
    public string Apellido { get; set; } = string.Empty;

    [Required(ErrorMessage = "El CUIL es obligatorio.")]
    [MaxLength(25)]
    public string CUIL { get; set; } = string.Empty;

    [Required(ErrorMessage = "La fecha de nacimiento es obligatoria.")]
    public DateTime FechaNacimiento { get; set; } = new DateTime(1988, 1, 1);

    [Required(ErrorMessage = "El celular / WhatsApp es obligatorio.")]
    [MaxLength(50)]
    public string CelularWhatsApp { get; set; } = string.Empty;

    [Required(ErrorMessage = "El correo electrónico es obligatorio.")]
    [EmailAddress(ErrorMessage = "Formato de correo inválido.")]
    [MaxLength(150)]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "El domicilio es obligatorio.")]
    [MaxLength(200)]
    public string Domicilio { get; set; } = string.Empty;

    [Required(ErrorMessage = "La localidad es obligatoria.")]
    [MaxLength(100)]
    public string Localidad { get; set; } = "Santa Rosa";

    [Required(ErrorMessage = "La provincia es obligatoria.")]
    [MaxLength(100)]
    public string Provincia { get; set; } = "La Pampa";

    [Required(ErrorMessage = "La patente del camión es obligatoria.")]
    [MaxLength(20)]
    public string PatenteCamion { get; set; } = string.Empty;

    [Range(100, 100000, ErrorMessage = "La capacidad de carga debe estar entre 100 kg y 100.000 kg.")]
    public decimal CapacidadCargaKg { get; set; } = 15000;

    [Range(1, 500, ErrorMessage = "La capacidad de volumen debe estar entre 1 m³ y 500 m³.")]
    public decimal CapacidadVolumenM3 { get; set; } = 60;

    public List<int> DocumentosIds { get; set; } = new();
}

public class ConfirmarEmailResponseDto
{
    public bool Exitoso { get; set; }
    public string Mensaje { get; set; } = string.Empty;
    public string? RazonSocial { get; set; }
    public bool YaConfirmado { get; set; }
}
