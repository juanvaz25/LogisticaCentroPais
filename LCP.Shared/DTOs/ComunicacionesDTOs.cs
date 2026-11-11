using System.ComponentModel.DataAnnotations;

namespace LCP.Shared.DTOs;

public class TareaCalendarioDto
{
    public int Id { get; set; }
    public string Titulo { get; set; } = string.Empty;
    public string Detalle { get; set; } = string.Empty;
    public DateOnly Fecha { get; set; }
    public TimeOnly HoraInicio { get; set; }
    public TimeOnly HoraFin { get; set; }
    public int ResponsableUsuarioId { get; set; }
    public string ResponsableNombre { get; set; } = string.Empty;
    public string ResponsableRol { get; set; } = string.Empty;
    public int? SolicitudOperacionId { get; set; }
    public string EstadoTarea { get; set; } = "Pendiente"; // Pendiente, EnCurso, Completada
    public string TipoActividad { get; set; } = "Tarea"; // Tarea, Reunion
    public string HorarioFormateado => $"{HoraInicio:HH:mm} a {HoraFin:HH:mm}";
}

public class CrearTareaCalendarioDto
{
    [Required(ErrorMessage = "El título es obligatorio.")]
    public string Titulo { get; set; } = string.Empty;

    public string Detalle { get; set; } = string.Empty;

    public DateOnly Fecha { get; set; }
    public TimeOnly HoraInicio { get; set; }
    public TimeOnly HoraFin { get; set; }

    [Required(ErrorMessage = "Debe asignar un responsable.")]
    public int ResponsableUsuarioId { get; set; }

    public int? SolicitudOperacionId { get; set; }

    public string TipoActividad { get; set; } = "Tarea"; // Tarea, Reunion
}

public class ActualizarEstadoTareaDto
{
    public int TareaId { get; set; }
    public string NuevoEstado { get; set; } = "Completada";
}

public class MensajeChatDto
{
    public int Id { get; set; }
    public string ConversacionId { get; set; } = string.Empty;
    public int RemitenteUsuarioId { get; set; }
    public string RemitenteNombre { get; set; } = string.Empty;
    public string RemitenteRol { get; set; } = string.Empty;
    public int? DestinatarioUsuarioId { get; set; }
    public int? SolicitudOperacionId { get; set; }
    public string TextoMensaje { get; set; } = string.Empty;
    public bool EsIncidenteRuta { get; set; }
    public bool Leido { get; set; }
    public DateTime FechaHoraEnvio { get; set; }
    public string HoraFormateada => FechaHoraEnvio.ToString("HH:mm");
}

public class EnviarMensajeDto
{
    [Required]
    public string ConversacionId { get; set; } = string.Empty;

    public int? DestinatarioUsuarioId { get; set; }
    public int? SolicitudOperacionId { get; set; }

    [Required(ErrorMessage = "El mensaje no puede estar vacío.")]
    public string TextoMensaje { get; set; } = string.Empty;

    public bool EsIncidenteRuta { get; set; } = false;
}

public class ConversacionResumenDto
{
    public string ConversacionId { get; set; } = string.Empty;
    public string Titulo { get; set; } = string.Empty;
    public string Subtitulo { get; set; } = string.Empty;
    public string UltimoMensaje { get; set; } = string.Empty;
    public string HoraUltimoMensaje { get; set; } = string.Empty;
    public int MensajesNoLeidos { get; set; }
    public bool Activo { get; set; }
    public bool Finalizada { get; set; }
    public int? SolicitudId { get; set; }
}

public class DocumentoAdjuntoDto
{
    public int Id { get; set; }
    public string TipoDocumento { get; set; } = string.Empty; // Remito, CartaDePorte, Factura, LicenciaConducir, SeguroCamion
    public string NombreArchivo { get; set; } = string.Empty;
    public string RutaUrl { get; set; } = string.Empty;
    public int? SolicitudOperacionId { get; set; }
    public int? LiquidacionId { get; set; }
    public int? ConductorId { get; set; }
    public int SubidoPorUsuarioId { get; set; }
    public string SubidoPorNombre { get; set; } = string.Empty;
    public DateTime FechaSubida { get; set; }
}

public class NotificacionOperacionDto
{
    public int Id { get; set; }
    public string Tipo { get; set; } = string.Empty; // "Arribo", "Despacho", "Turno", "Darsena", "Facturacion"
    public string Codigo { get; set; } = string.Empty;
    public string Titulo { get; set; } = string.Empty;
    public string Mensaje { get; set; } = string.Empty;
    public string Estado { get; set; } = string.Empty;
    public string Icono { get; set; } = "🔔";
    public string Url { get; set; } = "#";
    public DateTime FechaHora { get; set; }
    public string TiempoHace { get; set; } = string.Empty;
    public bool Leida { get; set; }
}

public class NotificacionMensajeDto
{
    public int Id { get; set; }
    public string RemitenteNombre { get; set; } = string.Empty;
    public string RemitenteRol { get; set; } = string.Empty;
    public string ConversacionId { get; set; } = string.Empty;
    public string Asunto { get; set; } = string.Empty;
    public string TextoMensaje { get; set; } = string.Empty;
    public string Url { get; set; } = "#";
    public DateTime FechaHora { get; set; }
    public string TiempoHace { get; set; } = string.Empty;
    public bool EsIncidente { get; set; }
    public bool Leida { get; set; }
}

