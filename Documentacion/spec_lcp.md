# Especificación Técnica y Reglas de Negocio para Desarrollo con IA (Vibe Coding) — Logística Centro País (LCP)

> **Propósito de este archivo**: Este documento es la **fuente única de verdad (Single Source of Truth)** para asistentes de programación con IA (Cursor, GitHub Copilot, Windsurf, Claude, ChatGPT). Cuando generes código para este proyecto, debés respetar estrictamente la separación de proyectos, la matriz de roles (RBAC), el modelo de datos y las máquinas de estado definidas a continuación. **Nunca mezcles vistas de distintos roles ni dejes flujos sin su transición de estado completa.**

## 1. Contexto Académico y Arquitectura de la Solución (.NET)

El sistema **Logística Centro País (LCP)** digitaliza un nodo logístico físico ubicado en **Santa Rosa, La Pampa (Argentina)** que conecta la Región Pampeana, Cuyo y la Patagonia.

Debido a que el sistema se presenta en dos cátedras distintas del **I.T.E.S** (*Programación III - Aplicaciones Web* con el Prof. Fabio Gross y *Programación III - Aplicaciones Móviles* con el Prof. Federico Trani), la solución (`LogisticaCentroPais.sln`) **DEBE** estar dividida en **dos proyectos Front-End independientes que consumen una misma Web API central**:

| Proyecto en la Solución `.sln` | Tipo de Proyecto .NET | Propósito y Alcance | Roles que operan en esta plataforma |
| :--- | :--- | :--- | :--- |
| **`LCP.Shared`** | Class Library (`.NET 8/9`) | Contiene todos los **DTOs**, **Enums de Estados**, constantes de **Roles** y validaciones (`DataAnnotations`) compartidas entre la API, MVC y Blazor. | Todos (Biblioteca transversal) |
| **`LCP.Api`** | ASP.NET Core Web API | Backend unificado. Expone controladores RESTful (`/api/...`), **SignalR Hubs** (`/hubs/patio` y `/hubs/chat`), lógica de negocio, **Entity Framework Core** + **SQL Server** y autenticación **JWT + Identity**. Se ejecuta en **Docker**. | Todos (Consumido vía `HttpClient`) |
| **`LCP.Web`** | ASP.NET Core MVC (Razor Views) | **Plataforma Web Administrativa y Portal de Clientes** (Cátedra: *Aplicaciones Web*). Optimizada para pantallas de escritorio, tableros de gestión de patio, grilla de facturación/auditoría y contratos. | `Publico`, `Administrador`, `Operador`, `Auditor`, `Cliente` |
| **`LCP.Mobile`** | Blazor Hybrid (.NET MAUI) o Blazor PWA | **Aplicación Móvil Operativa** (Cátedra: *Aplicaciones Móviles*). Optimizada para celulares y tablets en cabina de camión o en muelle de descarga. Consume la misma `LCP.Api` y los mismos Hubs de SignalR. | `Conductor` (principal), `Operador` (en muelle/depósito), `Cliente` (consultas rápidas) |

### Regla de Oro de Arquitectura para la IA
1. **Cero acceso directo a Base de Datos desde `LCP.Web` o `LCP.Mobile`**: Ni el proyecto MVC ni el proyecto Blazor deben instanciar `DbContext`. Ambos se comunican exclusivamente con `LCP.Api` mediante servicios tipados (`HttpClient` con token JWT en el header `Authorization: Bearer <token>`) y clientes de SignalR (`HubConnection`).
2. **Separación de Vistas por Rol**: **PROHIBIDO** renderizar el mismo menú lateral completo para todos los usuarios. El layout de `LCP.Web` (`_Layout.cshtml`) y el de `LCP.Mobile` (`MainLayout.razor` / `NavMenu.razor`) deben usar `User.IsInRole("...")` o `<AuthorizeView Roles="...">` para mostrar **únicamente** las secciones permitidas a ese rol.

## 2. Reglas de Negocio Centrales (Dominio LCP)

Para no programar funcionalidades a medias, toda implementación debe respetar estas 4 reglas de dominio:

### Regla 1: Alquiler de Dársenas por Contrato (Trimestral, Semestral o Anual)
* En LCP, una **Dársena** no es solo una puerta de paso: es un **módulo de carga, descarga y acopio asignado en alquiler** dentro del nodo logístico.
* Un **Cliente** (empresa, PyME, cooperativa o industria) alquila **una o varias dársenas** por un período fijo:
  * **Trimestral** (3 meses)
  * **Semestral** (6 meses)
  * **Anual** (12 meses)
* El **Administrador** configura los valores de tarifa de alquiler para cada plan (Trimestral, Semestral, Anual).
* Cuando el Cliente contrata el alquiler de una o más dársenas:
  1. Se crea el registro en `ContratosAlquilerDarsena` vinculando al `Cliente` con la(s) `Darsena(s)`.
  2. Se genera **automáticamente** una `Liquidacion` de tipo `AlquilerDarsena` en estado `PendienteDeAuditoria` por el monto del plan elegido.
  3. Una vez activo el contrato, esa dársena queda vinculada al Cliente para recibir sus camiones y almacenar su stock.

### Regla 2: Solicitudes de Arribo / Despacho y Transporte Dual (Chofer Propio vs. Flota LCP)
* El **Cliente** crea una **Solicitud de Operación** (`SolicitudOperacion`) sobre una de sus dársenas alquiladas, indicando si es:
  * **`ArriboIngreso`**: Llega un camión con mercadería para descargar y almacenar en la dársena del cliente.
  * **`DespachoRetiro`**: Sale mercadería almacenada en la dársena del cliente hacia un destino.
* Al crear la solicitud, el **Cliente** debe elegir obligatoriamente la **Modalidad de Transporte (`ModalidadTransporte`)**:
  1. **`ChoferPropio`**: El cliente utiliza su propio camión/conductor (seleccionándolo de su padrón de choferes propios o indicando los datos del conductor asignado).
  2. **`FlotaLCP`**: El cliente solicita que **Logística Centro País** le provea un camión y conductor de la flota del nodo. En este caso, la solicitud queda esperando que el **Operador** le asigne un **Conductor de Flota LCP** disponible y programe el viaje.

### Regla 3: Recepción en Dársena, Descarga por el Operador y Ordenamiento de Stock
* Cuando el camión (sea `ChoferPropio` o `FlotaLCP`) llega al predio en la fecha y hora de su turno:
  1. El **Conductor** (desde la app móvil Blazor) o el **Operador** marca el arribo al predio, pasando el estado de la dársena a **`Ocupada`** (notificado en tiempo real por SignalR).
  2. El **Operador** (y el personal interno de depósito asignado en el calendario, por ejemplo, *Martín López*) realiza físicamente la **descarga del pedido** (o la carga si es un despacho) y **ordena la mercadería dentro de la dársena alquilada por el cliente**.
  3. Al confirmar la descarga en el sistema, el **Operador** registra o actualiza los **Lotes de Inventario (`LoteInventario`)** asociados a esa dársena y a ese cliente, impactando inmediatamente en el stock visible por el Cliente.
  4. Al finalizar la operación y retirarse el camión, la dársena vuelve al estado **`Libre`** (o **`TurnoAsignado`** si tiene otro turno próximo) y la solicitud pasa a **`Completada`**.

### Regla 4: Liquidación Automática y Auditoría Obligatoria
* **Ningún cobro o factura se emite sin pasar por el Auditor.**
* El sistema genera registros en la tabla `Liquidaciones` de manera **100% automática** en dos momentos:
  1. **Al contratar o renovar alquiler de dársena(s)**: Calcula `CantidadDarsenas * TarifaPlan(Trimestral|Semestral|Anual)`.
  2. **Al completarse una Solicitud / Viaje (`EstadoSolicitud.Completada`)**: Calcula automáticamente el importe combinando las variables tarifarias configuradas por el Administrador:
     * Si usó **`FlotaLCP`**: Cobra tarifa por **Peso (kg/tn) + Volumen (m³) + Distancia (km)** más cargos operativos.
     * Si usó **`ChoferPropio`** o excedió estadía/acopio: Cobra tarifa operativa de movimiento de muelle / **Tiempo de depósito** según reglas vigentes.
* Toda liquidación recién calculada nace con `EstadoLiquidacion = PendienteDeAuditoria`.
* El **Auditor** es el **único rol** (junto al Administrador en modo supervisión) que puede revisar el desglose y la documentación adjunta (remitos, cartas de porte, facturas) y cambiar el estado de la liquidación a:
  * **`Validada`**: Aprobada y firme para facturación/cobro.
  * **`Observada`**: Rechazada temporalmente con un comentario de auditoría para su corrección o adjunción de comprobantes faltantes.

## 3. Matriz Estricta de Roles y Permisos (RBAC)

Usa exactamente estos nombres de constantes en `LCP.Shared.Constants.Roles`:
* `Administrador`
* `Operador`
* `Auditor`
* `Cliente`
* `Conductor`
* *(Público / No autenticado: endpoints con `[AllowAnonymous]`)*

| Módulo / Funcionalidad | Público (Anónimo) | Administrador (Interno) | Operador (Interno - Patio) | Auditor (Interno - Finanzas) | Cliente (Externo - Empresa) | Conductor (Externo - Chofer) |
| :--- | :---: | :---: | :---: | :---: | :---: | :---: |
| **Landing Page y Login** | Sí | Sí | Sí | Sí | Sí | Sí |
| **Postularse como Chofer LCP / Solicitar Alta Empresa** | **Crear** | Aprobar / Rechazar | Solo ver | No | No | No |
| **ABM Usuarios y Personal Interno del Nodo** | No | **CRUD Total** | Ver personal | No | No | No |
| **Configurar Tarifas (Planes Dársena, Peso, Volumen, Km)** | No | **CRUD Total** | Consultar | Consultar | Consultar planes | No |
| **Alquiler de Dársenas (Trimestral / Semestral / Anual)** | No | Administrar todos | Ver contratos activos | Ver contratos | **Solicitar / Ver propios** | No |
| **Tablero de Dársenas en Tiempo Real (SignalR)** | No | Ver y editar todo | **Cambiar estados y asignar turnos** | Solo lectura | Ver estado de **sus** dársenas | Ver dársena de **su** turno |
| **Solicitudes de Arribo / Despacho** | No | Ver todas | **Gestionar todas, asignar chofer LCP y descargar** | Solo lectura | **Crear solicitudes propias** (Chofer propio o Flota LCP) | Ver las asignadas a él |
| **Padrón de Choferes Propios del Cliente** | No | Ver todos | Ver todos | No | **ABM de sus propios choferes** | Ver su perfil |
| **Descarga de Camión y Ordenamiento de Stock en Dársena** | No | Supervisar | **Ejecutar descarga, cargar/mover lotes de stock** | Solo lectura | **Consultar su stock en tiempo real** y pedir órdenes de salida | Confirmar entrega en muelle |
| **Hoja de Ruta y Cambio de Estado del Viaje** | No | Supervisar | Asignar y monitorear | No | Ver tracking de sus pedidos | **Actualizar estado (`EnRuta`, `EnDarsena`, `Completado`)** |
| **Documentación (Remitos, Cartas de Porte, Licencias)** | Subir en alta | Ver todos | Subir/verificar remitos de recepción | **Auditar documentos respaldatorios** | **Adjuntar y descargar documentos de sus pedidos** | **Subir foto de remito firmado desde móvil** |
| **Liquidaciones y Facturación** | No | Supervisar todas | Ver estado operativo | **Validar u Observar liquidaciones (`PendienteDeAuditoria` → `Validada`)** | **Ver sus propias liquidaciones y comprobantes** | Ver sus viajes liquidados |
| **Calendario y Tareas de Depósito** | No | Ver global | **Crear y asignar tareas al personal (ej. preparar pedido)** | Ver agenda de auditoría | Ver calendario de sus turnos | **Ver su calendario de viajes/turnos** |
| **Chat e Incidentes en Vivo (SignalR)** | No | Acceso total | **Atender incidentes, reprogramar dársenas y chatear** | No | **Chatear con Operador sobre sus pedidos** | **Reportar incidentes en ruta y chatear con Operador** |

## 4. Máquinas de Estado y Enums (`LCP.Shared.Enums`)

Cuando programes en C#, definí estos `enum` en `LCP.Shared/Enums/` y respetá las transiciones permitidas:

```csharp
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
    Rechazada = 3
}

public enum PlanAlquilerDarsena
{
    Trimestral = 1,     // 3 meses
    Semestral = 2,      // 6 meses
    Anual = 3           // 12 meses
}

public enum EstadoContratoDarsena
{
    PendienteActivacion = 1,
    Activo = 2,
    Vencido = 3,
    Cancelado = 4
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
```

## 5. Modelo de Datos Relacional (SQL Server / Entity Framework Core)

El `LcpDbContext` en `LCP.Api` debe implementar las siguientes entidades principales y sus relaciones:

### 5.1. `Usuario` (Identity / Tabla Usuarios)
* `Id` (int / string PK)
* `Nombre` (string), `Apellido` (string), `Email` (string único), `PasswordHash` (string), `Telefono` (string)
* `Rol` (`RolUsuario`)
* `Activo` (bool)
* `ClienteId` (int? FK opcional — si es `RolUsuario.Cliente`, vincula a la empresa; si es `RolUsuario.Conductor` de tipo `PropioCliente`, vincula a qué cliente pertenece)
* `FechaAlta` (DateTime)

### 5.2. `ClienteEmpresa` (Empresas Clientes del Nodo)
* `Id` (int PK)
* `RazonSocial` (string — ej. *"Almacén El Molino"*), `CUIT` (string único)
* `Rubro` (string — PyME, Industria, Comercio, Cooperativa)
* `Domicilio` (string), `Localidad` (string), `Provincia` (string)
* `EmailContacto` (string), `TelefonoContacto` (string)
* `EstadoAlta` (`EstadoPostulacionAlta`)

### 5.3. `Conductor` (Padrón de Choferes: Flota LCP y Propios de Clientes)
* `Id` (int PK)
* `UsuarioId` (int? FK a `Usuario` una vez aprobada su cuenta)
* `TipoConductor` (`TipoConductor`: `FlotaLCP` o `PropioCliente`)
* `ClienteEmpresaId` (int? FK — `null` si pertenece a la Flota de LCP; con valor si es chofer propio de un Cliente)
* `Nombre` (string), `Apellido` (string), `CUIL` (string), `FechaNacimiento` (DateTime)
* `CelularWhatsApp` (string), `Email` (string), `Domicilio` (string), `Localidad` (string), `Provincia` (string)
* `PatenteCamion` (string), `CapacidadCargaKg` (decimal), `CapacidadVolumenM3` (decimal)
* `EstadoPostulacion` (`EstadoPostulacionAlta`)
* `DeclaracionJuradaAceptada` (bool)

### 5.4. `TarifaConfiguracion` (Reglas Tarifarias administradas por el Administrador)
* `Id` (int PK)
* `CodigoConcepto` (string único: `ALQUILER_TRIMESTRAL`, `ALQUILER_SEMESTRAL`, `ALQUILER_ANUAL`, `PRECIO_POR_KG`, `PRECIO_POR_M3`, `PRECIO_POR_KM`, `CARGO_DESCARGA_OPERADOR`, `CARGO_DIA_DEPOSITO`)
* `Descripcion` (string)
* `ValorUnitario` (decimal)
* `UltimaActualizacion` (DateTime), `ActualizadoPorUsuarioId` (int FK)

### 5.5. `Darsena` (Dársenas y Módulos de Acopio del Predio)
* `Id` (int PK)
* `NumeroDarsena` (int — ej. `1`, `2`, `3`, `4`, `5`, `6`), `Codigo` (string — ej. `"Dársena 1"`)
* `CapacidadMaximaPallets` (int), `CapacidadMaximaM3` (decimal)
* `EstadoActual` (`EstadoDarsena`: `Libre`, `TurnoAsignado`, `Ocupada`, `Mantenimiento`)
* `ClienteAlquilerActualId` (int? FK a `ClienteEmpresa` que tiene el contrato activo sobre esta dársena)
* `DetalleOperacionActual` (string? — ej. `"Retiro #1048 - Carlos Fernández"`)

### 5.6. `ContratoAlquilerDarsena` (Alquiler Trimestral, Semestral o Anual)
* `Id` (int PK)
* `ClienteEmpresaId` (int FK)
* `DarsenaId` (int FK)
* `Plan` (`PlanAlquilerDarsena`: `Trimestral`, `Semestral`, `Anual`)
* `FechaInicio` (DateTime), `FechaFin` (DateTime)
* `MontoTotalContrato` (decimal — calculado automáticamente según `TarifaConfiguracion`)
* `Estado` (`EstadoContratoDarsena`)

### 5.7. `SolicitudOperacion` (Pedidos de Arribo/Ingreso o Despacho/Retiro)
* `Id` (int PK — ej. `#1048`)
* `CodigoSolicitud` (string — ej. `"#1048"`)
* `ClienteEmpresaId` (int FK — ej. *Almacén El Molino*)
* `DarsenaId` (int FK — debe ser una dársena alquilada por ese Cliente)
* `TipoOperacion` (`TipoOperacionSolicitud`: `ArriboIngreso` o `DespachoRetiro`)
* `ModalidadTransporte` (`ModalidadTransporte`: `ChoferPropio` o `FlotaLCP`)
* `ConductorAsignadoId` (int? FK a `Conductor` — elegido por el Cliente si es `ChoferPropio`, o asignado por el Operador si pidió `FlotaLCP`)
* `ResponsableDepositoId` (int? FK a `PersonalInterno` / `Usuario` — ej. *Martín López*)
* `DescripcionMercaderia` (string — ej. `"24 frascos en 2 cajas"`)
* `PesoTotalKg` (decimal), `VolumenTotalM3` (decimal), `DistanciaEstimadaKm` (decimal)
* `FechaHoraTurnoProgramado` (DateTime)
* `FechaHoraIngresoReal` (DateTime?), `FechaHoraEgresoReal` (DateTime?)
* `Estado` (`EstadoSolicitud`: `Pendiente`, `Programada`, `EnCurso`, `Completada`, `Cancelada`)

### 5.8. `Viaje` (Hoja de Ruta y Seguimiento Móvil del Conductor)
* `Id` (int PK)
* `SolicitudOperacionId` (int FK 1:1 con `SolicitudOperacion`)
* `ConductorId` (int FK)
* `Origen` (string), `Destino` (string), `PuertaIngresoAsignada` (string — ej. `"Calle San Martín 245"`)
* `EstadoViaje` (`EstadoViaje`: `Asignado`, `EnRuta`, `ConIncidenteDemora`, `EnPredioDarsena`, `DescargadoCompletado`)
* `UltimaLatitud` (double?), `UltimaLongitud` (double?), `UltimaActualizacionGps` (DateTime?)
* `ObservacionesChofer` (string?)

### 5.9. `LoteInventario` (Stock Ordenado en la Dársena Alquilada por el Cliente)
* `Id` (int PK)
* `ClienteEmpresaId` (int FK)
* `DarsenaId` (int FK — dársena donde el Operador descargó y ordenó el lote)
* `SolicitudIngresoId` (int? FK a la `SolicitudOperacion` que originó el ingreso)
* `CodigoLote` (string), `ProductoDescripcion` (string — ej. `"Frascos conserveros x 12"`)
* `CantidadUnidades` (int), `CantidadBultosCajas` (int), `PesoKg` (decimal), `VolumenM3` (decimal)
* `UbicacionInternaDarsena` (string — ej. `"Pasillo A - Estante 2"`)
* `FechaIngreso` (DateTime), `OrdenadoPorOperadorId` (int FK)

### 5.10. `Liquidacion` (Facturación Calculada Automáticamente y Auditada por el Auditor)
* `Id` (int PK)
* `NumeroComprobante` (string — ej. `"0001-0042"`)
* `ClienteEmpresaId` (int FK)
* `TipoLiquidacion` (`TipoLiquidacion`: `CanonAlquilerDarsena` o `ServicioTransporteYPatio`)
* `ContratoAlquilerDarsenaId` (int? FK opcional)
* `SolicitudOperacionId` (int? FK opcional)
* `VariablesAplicadasResumen` (string — ej. `"Peso, volumen y distancia (Flota LCP)"` o `"Alquiler Trimestral Dársena 1"`)
* `SubtotalPeso` (decimal), `SubtotalVolumen` (decimal), `SubtotalDistancia` (decimal), `SubtotalTiempoDepositoOAlquiler` (decimal)
* `ImporteTotal` (decimal)
* `Estado` (`EstadoLiquidacion`: nace SIEMPRE en `PendienteDeAuditoria`; luego `Validada` u `Observada`)
* `AuditadoPorUsuarioId` (int? FK al `Auditor`), `FechaAuditoria` (DateTime?), `ObservacionAuditoria` (string?)

### 5.11. `TareaCalendario` (Agenda del Día y Asignación de Personal)
* `Id` (int PK)
* `Titulo` (string — ej. `"Preparar pedido #1048"`, `"Control de stock"`, `"Retiro del pedido #1048"`)
* `Detalle` (string — ej. `"Almacén El Molino - 24 frascos"`)
* `Fecha` (DateOnly), `HoraInicio` (TimeOnly), `HoraFin` (TimeOnly)
* `ResponsableUsuarioId` (int FK — ej. *Martín López*)
* `SolicitudOperacionId` (int? FK opcional)
* `EstadoTarea` (string: `"Pendiente"`, `"EnCurso"`, `"Completada"`)

### 5.12. `DocumentoAdjunto` y `MensajeChat`
* **`DocumentoAdjunto`**: `Id`, `TipoDocumento` (`Remito`, `CartaDePorte`, `Factura`, `ConstanciaEntrega`, `LicenciaConducir`, `SeguroCamion`), `NombreArchivo`, `RutaUrl`, `SolicitudOperacionId?`, `LiquidacionId?`, `ConductorId?`, `SubidoPorUsuarioId`, `FechaSubida`.
* **`MensajeChat`**: `Id`, `ConversacionId`, `RemitenteUsuarioId`, `DestinatarioUsuarioId?`, `SolicitudOperacionId?`, `TextoMensaje`, `EsIncidenteRuta` (bool), `Leido` (bool), `FechaHoraEnvio`.

## 6. Servicio de Cálculo Automático de Liquidaciones (Código de Referencia para `LCP.Api`)

Cuando una `SolicitudOperacion` pasa al estado `Completada` (luego de que el Operador confirma la descarga/carga en la dársena) o cuando se crea un `ContratoAlquilerDarsena`, el backend debe ejecutar esta lógica de cálculo y dejar el registro en `EstadoLiquidacion.PendienteDeAuditoria`:

```csharp
public class LiquidacionService : ILiquidacionService
{
    private readonly LcpDbContext _db;

    public LiquidacionService(LcpDbContext db) => _db = db;

    public async Task<Liquidacion> GenerarLiquidacionPorContratoDarsenaAsync(ContratoAlquilerDarsena contrato)
    {
        string codigoTarifa = contrato.Plan switch
        {
            PlanAlquilerDarsena.Trimestral => "ALQUILER_TRIMESTRAL",
            PlanAlquilerDarsena.Semestral  => "ALQUILER_SEMESTRAL",
            PlanAlquilerDarsena.Anual      => "ALQUILER_ANUAL",
            _ => throw new InvalidOperationException("Plan de alquiler inválido.")
        };

        decimal valorPlan = await _db.TarifasConfiguracion
            .Where(t => t.CodigoConcepto == codigoTarifa)
            .Select(t => t.ValorUnitario)
            .FirstAsync();

        var liquidacion = new Liquidacion
        {
            NumeroComprobante = await GenerarSiguienteComprobanteAsync(),
            ClienteEmpresaId = contrato.ClienteEmpresaId,
            TipoLiquidacion = TipoLiquidacion.CanonAlquilerDarsena,
            ContratoAlquilerDarsenaId = contrato.Id,
            VariablesAplicadasResumen = $"Alquiler {contrato.Plan} - Dársena #{contrato.DarsenaId}",
            SubtotalTiempoDepositoOAlquiler = valorPlan,
            ImporteTotal = valorPlan,
            Estado = EstadoLiquidacion.PendienteDeAuditoria // Regla de negocio: SIEMPRE queda pendiente de auditar
        };

        _db.Liquidaciones.Add(liquidacion);
        await _db.SaveChangesAsync();
        return liquidacion;
    }

    public async Task<Liquidacion> GenerarLiquidacionPorSolicitudCompletadaAsync(int solicitudId)
    {
        var solicitud = await _db.SolicitudesOperacion.FindAsync(solicitudId)
            ?? throw new KeyNotFoundException("Solicitud no encontrada.");

        var tarifas = await _db.TarifasConfiguracion
            .ToDictionaryAsync(t => t.CodigoConcepto, t => t.ValorUnitario);

        decimal subPeso = 0, subVolumen = 0, subDistancia = 0, subOperativo = 0;
        var variablesUsadas = new List<string>();

        // Cargo operativo de descarga/carga por el Operador en la dársena alquilada
        subOperativo = tarifas.GetValueOrDefault("CARGO_DESCARGA_OPERADOR", 15000m);
        variablesUsadas.Add("Operativa de dársena");

        // Si el cliente solicitó Flota de LCP, se liquida Peso + Volumen + Distancia
        if (solicitud.ModalidadTransporte == ModalidadTransporte.FlotaLCP)
        {
            subPeso = solicitud.PesoTotalKg * tarifas.GetValueOrDefault("PRECIO_POR_KG", 50m);
            subVolumen = solicitud.VolumenTotalM3 * tarifas.GetValueOrDefault("PRECIO_POR_M3", 8000m);
            subDistancia = solicitud.DistanciaEstimadaKm * tarifas.GetValueOrDefault("PRECIO_POR_KM", 1200m);
            variablesUsadas.Add("Peso, volumen y distancia (Flota LCP)");
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
            Estado = EstadoLiquidacion.PendienteDeAuditoria // El Auditor debe validarla
        };

        _db.Liquidaciones.Add(liquidacion);
        await _db.SaveChangesAsync();
        return liquidacion;
    }
}
```

## 7. Mapa de Controladores y Pantallas por Proyecto

### 7.1. Proyecto Web (`LCP.Web` — ASP.NET Core MVC)
* `HomeController` (`[AllowAnonymous]`):
  * `Index`: Landing Page institucional (El Nodo, Funcionalidades, Plataformas, Roles, estado ilustrativo de dársenas).
  * `PostulacionChofer`: Formulario público de alta de conductor con subida de fotos de licencia/documentación y declaración jurada.
  * `RegistroEmpresa`: Formulario público para que una PyME/empresa solicite alta como Cliente.
* `AuthController` (`[AllowAnonymous]`):
  * `Login` / `Logout`: Autenticación contra `/api/auth/login`, guardando el JWT en Cookie `HttpOnly` + Claims de Rol.
* `AdminController` (`[Authorize(Roles = "Administrador")]`):
  * `Tarifas`: Configuración de montos de planes Trimestral, Semestral y Anual de dársenas, y tarifas por kg, m³ y km.
  * `UsuariosYPersonal`: ABM de usuarios internos (`Operador`, `Auditor`, `Administrador`).
  * `Aprobaciones`: Bandeja para aprobar o rechazar altas de empresas clientes y postulaciones de choferes de la Flota LCP.
* `PatioController` (`[Authorize(Roles = "Operador,Administrador")]`):
  * `Darsenas`: Tablero en tiempo real de las dársenas (`Libre`, `TurnoAsignado`, `Ocupada`, `Mantenimiento`), turnos de hoy y botón para registrar ingreso/descarga/egreso de camión.
  * `DescargarYOrdenar`: Pantalla donde el Operador recibe el camión en la dársena alquilada por el cliente, registra la descarga de los bultos y ordena los lotes en el stock de esa dársena.
* `SolicitudesController` (`[Authorize(Roles = "Operador,Cliente,Administrador")]`):
  * **Si es `Cliente`**: Ve **solo sus solicitudes**, botón *"Nueva Solicitud"* (elige una de sus dársenas alquiladas, tipo `ArriboIngreso` o `DespachoRetiro`, y elige entre **`ChoferPropio`** o **`FlotaLCP`**).
  * **Si es `Operador`**: Ve **todas las solicitudes del nodo** filtradas por pestañas (`Todas`, `Pendientes`, `En curso`, `Completadas`), asigna chofer de Flota LCP cuando corresponde, asigna responsable de depósito (ej. Martín López) y cambia estados.
* `ClienteDarsenasController` (`[Authorize(Roles = "Cliente,Operador,Administrador")]`):
  * `MisContratos`: Permite al Cliente alquilar una o varias dársenas eligiendo el plan **Trimestral, Semestral o Anual**, ver la vigencia de su alquiler y consultar el **Inventario (Stock)** ordenado por el operador dentro de sus dársenas.
* `FacturacionController` (`[Authorize(Roles = "Auditor,Cliente,Administrador")]`):
  * **Si es `Auditor`**: Ve los 3 KPIs superiores (`Pendiente de auditoría`, `Validado este mes`, `Total liquidado`), la tabla de comprobantes con las variables aplicadas, revisa los remitos adjuntos y tiene los botones **`Validar Liquidación`** y **`Observar / Rechazar`**.
  * **Si es `Cliente`**: Ve únicamente el historial de sus propias liquidaciones y descarga sus comprobantes validados.
* `CalendarioController` (`[Authorize(Roles = "Operador,Administrador,Auditor,Cliente")]`):
  * Vista mensual y diaria de actividades (`08:00 Control de stock`, `10:00 Preparar pedido #1048`, `15:00 Retiro del pedido #1048`). Solo el `Operador` y el `Administrador` ven los botones *"Cargar actividad"*, *"Editar actividad"* y *"Marcar como completado"*.
* `InboxController` (`[Authorize]`):
  * Centro de chats en tiempo real con **SignalR** (`ChatIncidentesHub`) vinculando conversaciones al número de pedido (ej. `Retiro del pedido #1048 - Carlos Fernández`).

### 7.2. Proyecto Móvil (`LCP.Mobile` — Blazor Hybrid / MAUI o Blazor PWA)
* `/login`: Inicio de sesión móvil con almacenamiento seguro del JWT.
* **Vistas para el rol `Conductor` (tanto de Flota LCP como Chofer Propio de un Cliente)**:
  * `/mis-viajes`: Lista de viajes asignados hoy y próximos turnos de arribo/retiro, indicando puerta de ingreso (ej. *San Martín 245*) y dársena asignada.
  * `/viaje/{id}`: Detalle de hoja de ruta con botones grandes de acción rápida para cambiar estado en tiempo real (`Iniciar Viaje / En Ruta` → `Llegué al Predio / En Dársena` → `Descarga Finalizada`), más botón para sacar foto y adjuntar el **Remito / Constancia firmada**.
  * `/calendario-chofer`: Agenda móvil con sus horarios de carga y descarga.
  * `/chat-operador`: Chat directo en tiempo real (SignalR) con el Operador de turno para avisar *"Estoy llegando al depósito en 5 minutos"* o reportar un incidente/demora en ruta.
* **Vistas para el rol `Operador` en dispositivo móvil (en muelle/depósito)**:
  * `/muelle-recepcion`: Escaneo/selección rápida del turno en la dársena, confirmación de descarga del camión y carga rápida de bultos/cajas al stock de la dársena del cliente.
  * `/mis-tareas`: Checklist móvil de las tareas asignadas en el día (ej. *"Preparar pedido #1048 - 24 frascos"*).
* **Vistas para el rol `Cliente` en dispositivo móvil**:
  * `/mi-stock-movil`: Consulta rápida de mercadería disponible en sus dársenas alquiladas y estado en vivo de sus envíos en curso.

## 8. Prompt Listo para Copiar y Pegar en tu Asistente de IA (Cursor / Copilot)

Cuando empieces una sesión de código, pegale este bloque a la IA:

> Actuá como Arquitecto de Software Senior en .NET 8/9. Estamos desarrollando **Logística Centro País (LCP)** basándonos estrictamente en el archivo `SPEC_VIBE_CODING_LCP.md`.
> Recordá siempre estas 5 reglas innegociables antes de escribir cualquier archivo:
> 1. **Dos Front-Ends, una Web API**: Toda la persistencia (EF Core + SQL Server) y los Hubs de SignalR viven en `LCP.Api`. El proyecto `LCP.Web` (ASP.NET Core MVC) y el proyecto `LCP.Mobile` (Blazor) consumen `LCP.Api` vía `HttpClient` usando los DTOs de `LCP.Shared`.
> 2. **Roles estrictamente diferenciados**: Aplicá `[Authorize(Roles = "...")]` en cada endpoint y filtrá cada vista/menú por rol (`Administrador`, `Operador`, `Auditor`, `Cliente`, `Conductor`). Un Cliente solo puede ver sus propias dársenas alquiladas, su propio stock y sus propias solicitudes/liquidaciones. El Auditor solo valida u observa liquidaciones y reportes; no administra personal ni dársenas.
> 3. **Dársenas en Alquiler**: Los Clientes alquilan una o varias dársenas con plan `Trimestral`, `Semestral` o `Anual`. Cuando llega un camión al predio, el `Operador` realiza la descarga y ordena la mercadería dentro de la dársena alquilada por ese cliente, actualizando `LoteInventario`.
> 4. **Chofer Propio vs. Flota LCP**: Al crear una `SolicitudOperacion`, el `Cliente` elige si envía un `ChoferPropio` o si solicita `FlotaLCP` (en cuyo caso el `Operador` asigna un conductor de la flota del nodo).
> 5. **Liquidación Automática + Auditoría**: Al activarse un alquiler de dársena o completarse una solicitud/viaje, `LiquidacionService` calcula automáticamente el monto y crea la `Liquidacion` en estado `PendienteDeAuditoria`. Solo el `Auditor` puede pasarla a `Validada` u `Observada`.
