// ==========================================================================
// Logística Centro País (LCP) - SignalR Real-Time Client Helper
// ==========================================================================

const LCP_API_URL = "http://localhost:5152";

function getCookie(name) {
    const value = `; ${document.cookie}`;
    const parts = value.split(`; ${name}=`);
    if (parts.length === 2) return parts.pop().split(';').shift();
    return null;
}

// 1. Conexión al Hub de Patio en tiempo real
let patioConnection = null;

function iniciarPatioHub(onDarsenaActualizada) {
    if (typeof signalR === "undefined") {
        console.warn("SignalR library not loaded yet.");
        return;
    }

    const token = getCookie("LCP_JWT_TOKEN");

    patioConnection = new signalR.HubConnectionBuilder()
        .withUrl(`${LCP_API_URL}/hubs/patio`, {
            accessTokenFactory: () => token || ""
        })
        .withAutomaticReconnect()
        .build();

    patioConnection.on("DarsenaActualizada", function (darsena) {
        console.log("Notificación en tiempo real: Dársena actualizada", darsena);
        if (typeof onDarsenaActualizada === "function") {
            onDarsenaActualizada(darsena);
        }
    });

    patioConnection.start()
        .then(() => {
            console.log("Conectado al Hub de Patio (SignalR).");
            actualizarIndicadorSignalR(true);
            patioConnection.invoke("UnirseGrupoPatio").catch(err => console.error(err));
        })
        .catch(err => {
            console.error("Error al conectar con PatioHub:", err);
            actualizarIndicadorSignalR(false);
        });
}

// 2. Conexión al Hub de Chat e Incidentes
let chatConnection = null;

function iniciarChatHub(conversacionId, onMensajeRecibido, onAlertaIncidente, onChatFinalizado) {
    if (typeof signalR === "undefined") return;

    const token = getCookie("LCP_JWT_TOKEN");

    chatConnection = new signalR.HubConnectionBuilder()
        .withUrl(`${LCP_API_URL}/hubs/chat`, {
            accessTokenFactory: () => token || ""
        })
        .withAutomaticReconnect()
        .build();

    chatConnection.on("RecibirMensaje", function (mensaje) {
        console.log("Mensaje de chat recibido vía SignalR:", mensaje);
        if (typeof onMensajeRecibido === "function") {
            onMensajeRecibido(mensaje);
        }
        if (typeof window.handleFbMessageReceived === "function") {
            window.handleFbMessageReceived(mensaje);
        }
    });

    chatConnection.on("AlertaIncidente", function (incidente) {
        console.warn("Alerta de Incidente recibida:", incidente);
        if (typeof window.mostrarPopupNotificacion === "function") {
            window.mostrarPopupNotificacion({
                tipo: 'cancelado',
                titulo: '🚨 Alerta de Incidente en Ruta',
                mensaje: `${incidente.remitenteNombre || 'Chofer'}: ${incidente.textoMensaje || 'Demora reportada'}`,
                duracion: 6000
            });
        }
        if (typeof onAlertaIncidente === "function") {
            onAlertaIncidente(incidente);
        }
    });

    chatConnection.on("ChatFinalizado", function (info) {
        console.log("Evento ChatFinalizado recibido:", info);
        if (typeof window.mostrarPopupNotificacion === "function") {
            window.mostrarPopupNotificacion({
                tipo: 'estado',
                titulo: '🔒 Sala de Operación Finalizada',
                mensaje: info?.mensaje || 'La operación concluyó y la sala fue cerrada.',
                duracion: 5000
            });
        }
        if (typeof onChatFinalizado === "function") {
            onChatFinalizado(info);
        }
    });

    chatConnection.start()
        .then(() => {
            console.log("Conectado al Hub de Chat (SignalR).");
            if (conversacionId) {
                chatConnection.invoke("UnirseConversacion", conversacionId).catch(err => console.error(err));
            }
        })
        .catch(err => {
            console.error("Error al conectar con ChatIncidentesHub:", err);
        });
}

function actualizarIndicadorSignalR(conectado) {
    const el = document.getElementById("signalr-status-badge");
    if (!el) return;
    if (conectado) {
        el.innerHTML = '<span class="signalr-pulse"></span> Patio en vivo';
        el.className = "signalr-live-pill";
    } else {
        el.innerHTML = '<span style="width:8px;height:8px;background:#c23d3d;border-radius:50%;display:inline-block;"></span> Desconectado';
        el.className = "signalr-live-pill";
        el.style.backgroundColor = "#FBE8E8";
        el.style.color = "#A12323";
    }
}
