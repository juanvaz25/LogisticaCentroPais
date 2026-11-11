namespace LCP.Shared.Enums;

public enum RolUsuario
{
    Administrador = 1,
    Operador = 2,
    Auditor = 3,
    Cliente = 4,
    Conductor = 5
}

public enum TipoConductor
{
    FlotaLCP = 1,       // Chofer perteneciente o adherido a la flota de Logística Centro País
    PropioCliente = 2   // Chofer perteneciente a la empresa de un Cliente específico (ClienteId != null)
}

public enum EstadoPostulacionAlta
{
    PendienteRevision = 1,
    Aprobada = 2,
    Rechazada = 3,
    PendienteConfirmacionEmail = 4
}

public enum PlanAlquilerDarsena
{
    Trimestral = 1,     // 3 meses
    Semestral = 2,      // 6 meses
    Anual = 3           // 12 meses
}

public enum EstadoContratoDarsena
{
    PendienteRevision = 1,                 // 1. Solicitud enviada por el cliente, pendiente de evaluación técnica del Auditor
    Activo = 2,                           // 4. Factura verificada y Dársena habilitada formalmente por el Auditor para operar y cargar stock
    Vencido = 3,                          // Vencimiento del plazo contratado
    Cancelado = 4,                        // Cancelado administrativamente
    AprobadoPendientePago = 5,            // 2. Aprobado técnicamente por el Auditor, esperando que el cliente cargue la factura/comprobante de pago
    Rechazado = 6,                        // Rechazado por el Auditor
    FacturaCargadaPendienteHabilitacion = 7 // 3. El cliente ya cargó la factura, esperando que el Auditor la revise y habilite la dársena
}

public enum EstadoDarsena
{
    Libre = 1,          // Disponible para recibir turno
    TurnoAsignado = 2,  // Reservada para un arribo o despacho próximo
    Ocupada = 3,        // Camión actualmente en muelle descargando/cargando
    Mantenimiento = 4   // Fuera de servicio temporalmente
}

public enum TipoOperacionSolicitud
{
    ArriboIngreso = 1,  // Llega mercadería en camión -> Operador descarga y ordena en dársena del cliente
    DespachoRetiro = 2  // Se prepara pedido del stock de la dársena -> Se carga al camión y sale
}

public enum ModalidadTransporte
{
    ChoferPropio = 1,   // El cliente manda su propio camión/chofer
    FlotaLCP = 2        // El cliente pide que LCP le asigne un camión/chofer de su flota
}

public enum EstadoSolicitud
{
    Pendiente = 1,      // Creada por el Cliente; esperando asignación de turno/chofer LCP por el Operador
    Programada = 2,     // Con dársena, horario, chofer y responsable de depósito asignados
    EnCurso = 3,        // Viaje en tránsito o camión operando en dársena
    Completada = 4,     // Mercadería descargada/despachada, stock actualizado y liquidación automática generada
    Cancelada = 5
}

public enum EstadoViaje
{
    Asignado = 1,
    EnRuta = 2,
    ConIncidenteDemora = 3,
    EnPredioDarsena = 4,
    DescargadoCompletado = 5
}

public enum TipoLiquidacion
{
    CanonAlquilerDarsena = 1,   // Por contrato Trimestral, Semestral o Anual
    ServicioTransporteYPatio = 2 // Por viaje Flota LCP (Peso, Volumen, Distancia) o movimiento/estadía en depósito
}

public enum EstadoLiquidacion
{
    PendienteDeAuditoria = 1,   // Estado inicial obligatorio al calcularse automáticamente
    Validada = 2,               // Aprobada por el Auditor
    Observada = 3               // Rechazada/observada por el Auditor (requiere corrección o comprobante)
}
