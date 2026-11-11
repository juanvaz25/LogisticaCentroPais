using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using LCP.Api.Data;
using LCP.Shared.Constants;

namespace LCP.Api.Services;

public class ChatAuthorizationService : IChatAuthorizationService
{
    private readonly LcpDbContext _db;

    public ChatAuthorizationService(LcpDbContext db)
    {
        _db = db;
    }

    public async Task<bool> PuedeAccederAConversacionAsync(string conversacionId, int userId, ClaimsPrincipal user)
    {
        if (string.IsNullOrWhiteSpace(conversacionId) || userId <= 0 || user == null)
        {
            return false;
        }

        // 1. CHAT DIRECTO UNITARIO (1 a 1): DIRECT-{u1}-{u2}
        // Privacidad estricta: Únicamente los dos participantes legítimos pueden ver o participar.
        if (conversacionId.StartsWith("DIRECT-", StringComparison.OrdinalIgnoreCase))
        {
            var parts = conversacionId.Split('-');
            if (parts.Length == 3 &&
                int.TryParse(parts[1], out int u1) &&
                int.TryParse(parts[2], out int u2))
            {
                return userId == u1 || userId == u2;
            }
            return false;
        }

        // 2. CHAT GRUPAL TRIPARTITO DE OPERACIÓN: GRP-SOL-{solId}
        // Privacidad estricta: Solo Operador/Admin de LCP, el Cliente de la operación,
        // y el Conductor asignado a la operación. Ningún otro usuario ajeno.
        if (conversacionId.StartsWith("GRP-SOL-", StringComparison.OrdinalIgnoreCase))
        {
            var idPart = conversacionId.Substring("GRP-SOL-".Length);
            if (!int.TryParse(idPart, out int solId))
            {
                return false;
            }

            var sol = await _db.SolicitudesOperacion
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.Id == solId);

            if (sol == null)
            {
                return false;
            }

            // Administrador y Operador gestionan las operaciones logísticas del nodo
            if (user.IsInRole(Roles.Administrador) || user.IsInRole(Roles.Operador))
            {
                return true;
            }

            var userEmail = user.FindFirstValue(ClaimTypes.Email);

            // Cliente: solo si la solicitud pertenece a su empresa cliente
            if (user.IsInRole(Roles.Cliente))
            {
                int clienteId = 0;
                var clienteIdClaim = user.FindFirstValue("ClienteId");
                if (int.TryParse(clienteIdClaim, out int cid))
                {
                    clienteId = cid;
                }
                else
                {
                    var usr = await _db.Usuarios
                        .AsNoTracking()
                        .FirstOrDefaultAsync(u => u.Id == userId);
                    if (usr?.ClienteId != null)
                    {
                        clienteId = usr.ClienteId.Value;
                    }
                }

                return clienteId > 0 && sol.ClienteEmpresaId == clienteId;
            }

            // Conductor: solo si está asignado a la misión / solicitud
            if (user.IsInRole(Roles.Conductor))
            {
                var cond = await _db.Conductores
                    .AsNoTracking()
                    .FirstOrDefaultAsync(c => c.UsuarioId == userId || (userEmail != null && c.Email == userEmail));

                return cond != null && sol.ConductorAsignadoId == cond.Id;
            }

            // Cualquier otro rol (ej. Auditor) no participa en la sala operativa
            return false;
        }

        return false;
    }
}

