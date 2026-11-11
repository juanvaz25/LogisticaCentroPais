using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using LCP.Api.Data.Entities;
using LCP.Shared.Constants;
using LCP.Shared.Enums;

namespace LCP.Api.Data;

public static class DbInitializer
{
    public static async Task EnsureSchemaAsync(LcpDbContext context)
    {
        await context.Database.EnsureCreatedAsync();

        try
        {
            await context.Database.ExecuteSqlRawAsync(@"
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('SolicitudesOperacion') AND name = 'LoteInventarioId')
                BEGIN
                    ALTER TABLE SolicitudesOperacion ADD LoteInventarioId INT NULL;
                    ALTER TABLE SolicitudesOperacion ADD CantidadRetirada INT NULL;
                    ALTER TABLE SolicitudesOperacion ADD CiudadOrigen NVARCHAR(150) NULL;
                    ALTER TABLE SolicitudesOperacion ADD CiudadDestino NVARCHAR(150) NULL;
                    ALTER TABLE SolicitudesOperacion ADD FechaSalidaCliente DATETIME2 NULL;
                    ALTER TABLE SolicitudesOperacion ADD ObservacionesOperador NVARCHAR(500) NULL;
                END
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Usuarios') AND name = 'DebeCambiarPassword')
                BEGIN
                    ALTER TABLE Usuarios ADD DebeCambiarPassword BIT NOT NULL DEFAULT 0;
                END
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Usuarios') AND name = 'Dni')
                BEGIN
                    ALTER TABLE Usuarios ADD Dni NVARCHAR(25) NULL;
                    ALTER TABLE Usuarios ADD FechaNacimiento DATETIME2 NULL;
                    ALTER TABLE Usuarios ADD Direccion NVARCHAR(200) NULL;
                    ALTER TABLE Usuarios ADD Localidad NVARCHAR(100) NULL;
                    ALTER TABLE Usuarios ADD Provincia NVARCHAR(100) NULL;
                    ALTER TABLE Usuarios ADD CodigoPostal NVARCHAR(20) NULL;
                    ALTER TABLE Usuarios ADD EstadoCivil NVARCHAR(50) NULL;
                    ALTER TABLE Usuarios ADD ContactoEmergenciaNombre NVARCHAR(150) NULL;
                    ALTER TABLE Usuarios ADD ContactoEmergenciaTelefono NVARCHAR(50) NULL;
                    ALTER TABLE Usuarios ADD PuestoCargo NVARCHAR(100) NULL;
                    ALTER TABLE Usuarios ADD FechaIngreso DATETIME2 NULL;
                    ALTER TABLE Usuarios ADD ModalidadContratacion NVARCHAR(100) NULL;
                    ALTER TABLE Usuarios ADD ObservacionesLegajo NVARCHAR(500) NULL;
                    ALTER TABLE Usuarios ADD DocumentoIdentidadUrl NVARCHAR(500) NULL;
                    ALTER TABLE Usuarios ADD CvArchivoUrl NVARCHAR(500) NULL;
                    ALTER TABLE Usuarios ADD EmailConfirmado BIT NOT NULL DEFAULT 1;
                    ALTER TABLE Usuarios ADD TokenConfirmacionEmail NVARCHAR(100) NULL;
                    ALTER TABLE Usuarios ADD FechaConfirmacionEmail DATETIME2 NULL;
                END
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('ContratosAlquilerDarsena') AND name = 'MercaderiaAlmacenar')
                BEGIN
                    ALTER TABLE ContratosAlquilerDarsena ADD MercaderiaAlmacenar NVARCHAR(200) NULL;
                    ALTER TABLE ContratosAlquilerDarsena ADD TipoAlmacenamiento NVARCHAR(100) NULL;
                    ALTER TABLE ContratosAlquilerDarsena ADD ConsideracionesEspeciales NVARCHAR(500) NULL;
                    ALTER TABLE ContratosAlquilerDarsena ADD ObservacionAuditor NVARCHAR(500) NULL;
                    ALTER TABLE ContratosAlquilerDarsena ADD ComprobantePagoUrl NVARCHAR(250) NULL;
                    ALTER TABLE ContratosAlquilerDarsena ADD FechaAprobacionAuditor DATETIME2 NULL;
                    ALTER TABLE ContratosAlquilerDarsena ADD FechaPagoFactura DATETIME2 NULL;
                END

                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('ClientesEmpresa') AND name = 'EmailConfirmado')
                BEGIN
                    ALTER TABLE ClientesEmpresa ADD EmailConfirmado BIT NOT NULL DEFAULT 0;
                    ALTER TABLE ClientesEmpresa ADD TokenConfirmacionEmail NVARCHAR(100) NULL;
                    ALTER TABLE ClientesEmpresa ADD FechaConfirmacionEmail DATETIME2 NULL;
                END

                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Conductores') AND name = 'EmailConfirmado')
                BEGIN
                    ALTER TABLE Conductores ADD EmailConfirmado BIT NOT NULL DEFAULT 0;
                    ALTER TABLE Conductores ADD TokenConfirmacionEmail NVARCHAR(100) NULL;
                    ALTER TABLE Conductores ADD FechaConfirmacionEmail DATETIME2 NULL;
                    ALTER TABLE Conductores ADD LicenciaArchivoUrl NVARCHAR(500) NULL;
                    ALTER TABLE Conductores ADD SeguroCamionArchivoUrl NVARCHAR(500) NULL;
                END

                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('TareasCalendario') AND name = 'TipoActividad')
                BEGIN
                    ALTER TABLE TareasCalendario ADD TipoActividad NVARCHAR(50) NOT NULL DEFAULT 'Tarea';
                END

                -- Asegurar que filas existentes no tengan NULL en campos de texto
                UPDATE ContratosAlquilerDarsena 
                SET MercaderiaAlmacenar = ISNULL(MercaderiaAlmacenar, 'Carga General'),
                    TipoAlmacenamiento = ISNULL(TipoAlmacenamiento, 'Ambiente')
                WHERE MercaderiaAlmacenar IS NULL OR TipoAlmacenamiento IS NULL;

                UPDATE Usuarios
                SET EmailConfirmado = 1
                WHERE EmailConfirmado = 0 AND TokenConfirmacionEmail IS NULL;
            ");
        }
        catch { }
    }

    public static async Task SeedAsync(LcpDbContext context)
    {
        await EnsureSchemaAsync(context);

        try
        {
            await context.Database.ExecuteSqlRawAsync(@"
                -- Eliminar usuarios provisionales de clientes cuya empresa haya sido rechazada
                DELETE FROM [Usuarios] 
                WHERE ClienteId IN (SELECT Id FROM [ClientesEmpresa] WHERE EstadoAlta = 3);
            ");
        }
        catch { }

        // 1. Asegurar Admin
        var adminUser = await context.Usuarios.FirstOrDefaultAsync(u => u.Email == "admin@lcp.com");
        if (adminUser == null)
        {
            adminUser = new Usuario
            {
                Nombre = "Roberto",
                Apellido = "Gómez",
                Email = "admin@lcp.com",
                PasswordHash = HashPassword("Admin123!"),
                Telefono = "2954-600100",
                Rol = RolUsuario.Administrador,
                Activo = true,
                EmailConfirmado = true,
                FechaAlta = DateTime.UtcNow
            };
            context.Usuarios.Add(adminUser);
            await context.SaveChangesAsync();
        }

        // 2. Asegurar Operador Demo
        if (!await context.Usuarios.AnyAsync(u => u.Email == "operador@lcp.com"))
        {
            context.Usuarios.Add(new Usuario
            {
                Nombre = "Martín",
                Apellido = "Benítez",
                Email = "operador@lcp.com",
                PasswordHash = HashPassword("Operador123!"),
                Telefono = "2954-600200",
                Rol = RolUsuario.Operador,
                Activo = true,
                EmailConfirmado = true,
                FechaAlta = DateTime.UtcNow
            });
        }

        // 3. Asegurar Auditor Demo
        if (!await context.Usuarios.AnyAsync(u => u.Email == "auditor@lcp.com"))
        {
            context.Usuarios.Add(new Usuario
            {
                Nombre = "Lucía",
                Apellido = "Pereyra",
                Email = "auditor@lcp.com",
                PasswordHash = HashPassword("Auditor123!"),
                Telefono = "2954-600300",
                Rol = RolUsuario.Auditor,
                Activo = true,
                EmailConfirmado = true,
                FechaAlta = DateTime.UtcNow
            });
        }

        // 4. Asegurar Empresa Cliente y Usuario Cliente Demo
        var clienteEmpresa = await context.ClientesEmpresa.FirstOrDefaultAsync(c => c.CUIT == "30-71122334-9");
        if (clienteEmpresa == null)
        {
            clienteEmpresa = new ClienteEmpresa
            {
                RazonSocial = "Distribuidora Pampeana S.A.",
                CUIT = "30-71122334-9",
                Rubro = "Alimentos y Bebidas",
                Domicilio = "Av. Spinetto 1420",
                Localidad = "Santa Rosa",
                Provincia = "La Pampa",
                EmailContacto = "cliente@pampeana.com",
                TelefonoContacto = "2954-411223",
                EstadoAlta = EstadoPostulacionAlta.Aprobada,
                FechaSolicitud = DateTime.UtcNow,
                EmailConfirmado = true,
                FechaConfirmacionEmail = DateTime.UtcNow
            };
            context.ClientesEmpresa.Add(clienteEmpresa);
            await context.SaveChangesAsync();
        }

        var clienteUser = await context.Usuarios.FirstOrDefaultAsync(u => u.Email == "cliente@pampeana.com");
        if (clienteUser == null)
        {
            clienteUser = new Usuario
            {
                Nombre = "Carlos",
                Apellido = "Rossi",
                Email = "cliente@pampeana.com",
                PasswordHash = HashPassword("Cliente123!"),
                Telefono = "2954-411223",
                Rol = RolUsuario.Cliente,
                ClienteId = clienteEmpresa.Id,
                Activo = true,
                EmailConfirmado = true,
                FechaAlta = DateTime.UtcNow
            };
            context.Usuarios.Add(clienteUser);
            await context.SaveChangesAsync();
        }

        // 5. Asegurar Conductor Flota LCP Demo
        var conductorUser = await context.Usuarios.FirstOrDefaultAsync(u => u.Email == "conductor@flota.com");
        if (conductorUser == null)
        {
            conductorUser = new Usuario
            {
                Nombre = "Esteban",
                Apellido = "Morales",
                Email = "conductor@flota.com",
                PasswordHash = HashPassword("Chofer123!"),
                Telefono = "2954-555888",
                Rol = RolUsuario.Conductor,
                Activo = true,
                EmailConfirmado = true,
                FechaAlta = DateTime.UtcNow
            };
            context.Usuarios.Add(conductorUser);
            await context.SaveChangesAsync();
        }

        if (!await context.Conductores.AnyAsync(c => c.UsuarioId == conductorUser.Id || c.Email == "conductor@flota.com"))
        {
            context.Conductores.Add(new Conductor
            {
                Nombre = "Esteban",
                Apellido = "Morales",
                CUIL = "20-33445566-7",
                Email = "conductor@flota.com",
                CelularWhatsApp = "2954-555888",
                PatenteCamion = "AF-123-LP",
                CapacidadCargaKg = 24000m,
                CapacidadVolumenM3 = 85m,
                TipoConductor = TipoConductor.FlotaLCP,
                EstadoPostulacion = EstadoPostulacionAlta.Aprobada,
                FechaPostulacion = DateTime.UtcNow,
                UsuarioId = conductorUser.Id,
                EmailConfirmado = true
            });
        }

        // Si existen usuarios previos creados en pruebas manuales (juanjosev259...), asegurar que también puedan iniciar sesión con Admin123!
        var manualUsers = await context.Usuarios.Where(u => u.Email.StartsWith("juanjosev259")).ToListAsync();
        foreach (var u in manualUsers)
        {
            // Dejar HashPassword("Admin123!") por si el usuario lo olvidó
            u.PasswordHash = HashPassword("Admin123!");
            u.Activo = true;
            u.EmailConfirmado = true;
        }

        // 6. Tarifas si no existen
        if (!await context.TarifasConfiguracion.AnyAsync())
        {
            var tarifas = new List<TarifaConfiguracion>
            {
                new() { CodigoConcepto = TarifaCodigos.AlquilerTrimestral, Descripcion = "Alquiler de Dársena Plan Trimestral (3 meses)", ValorUnitario = 450000m, ActualizadoPorUsuarioId = adminUser.Id },
                new() { CodigoConcepto = TarifaCodigos.AlquilerSemestral, Descripcion = "Alquiler de Dársena Plan Semestral (6 meses)", ValorUnitario = 820000m, ActualizadoPorUsuarioId = adminUser.Id },
                new() { CodigoConcepto = TarifaCodigos.AlquilerAnual, Descripcion = "Alquiler de Dársena Plan Anual (12 meses)", ValorUnitario = 1500000m, ActualizadoPorUsuarioId = adminUser.Id },
                new() { CodigoConcepto = TarifaCodigos.PrecioPorKg, Descripcion = "Tarifa Flota LCP por Kilogramo transportado", ValorUnitario = 50m, ActualizadoPorUsuarioId = adminUser.Id },
                new() { CodigoConcepto = TarifaCodigos.PrecioPorM3, Descripcion = "Tarifa Flota LCP por Metro Cúbico transportado", ValorUnitario = 8000m, ActualizadoPorUsuarioId = adminUser.Id },
                new() { CodigoConcepto = TarifaCodigos.PrecioPorKm, Descripcion = "Tarifa Flota LCP por Kilómetro recorrido", ValorUnitario = 1200m, ActualizadoPorUsuarioId = adminUser.Id },
                new() { CodigoConcepto = TarifaCodigos.CargoDescargaOperador, Descripcion = "Cargo operativo muelle descarga/carga y ordenamiento", ValorUnitario = 15000m, ActualizadoPorUsuarioId = adminUser.Id },
                new() { CodigoConcepto = TarifaCodigos.CargoDiaDeposito, Descripcion = "Cargo por estadía extraordinaria de acopio por día", ValorUnitario = 5000m, ActualizadoPorUsuarioId = adminUser.Id }
            };
            context.TarifasConfiguracion.AddRange(tarifas);
        }

        // 7. Dársenas si no existen
        if (!await context.Darsenas.AnyAsync())
        {
            var d1 = new Darsena { NumeroDarsena = 1, Codigo = "Dársena 1", CapacidadMaximaPallets = 32, CapacidadMaximaM3 = 140m, EstadoActual = EstadoDarsena.Libre, DetalleOperacionActual = "Disponible ahora (Sin alquiler)" };
            var d2 = new Darsena { NumeroDarsena = 2, Codigo = "Dársena 2", CapacidadMaximaPallets = 30, CapacidadMaximaM3 = 120m, EstadoActual = EstadoDarsena.Libre, DetalleOperacionActual = "Disponible ahora (Sin alquiler)" };
            var d3 = new Darsena { NumeroDarsena = 3, Codigo = "Dársena 3", CapacidadMaximaPallets = 28, CapacidadMaximaM3 = 110m, EstadoActual = EstadoDarsena.Libre, DetalleOperacionActual = "Disponible ahora (Sin alquiler)" };
            var d4 = new Darsena { NumeroDarsena = 4, Codigo = "Dársena 4", CapacidadMaximaPallets = 36, CapacidadMaximaM3 = 150m, EstadoActual = EstadoDarsena.Libre, DetalleOperacionActual = "Disponible ahora (Sin alquiler)" };
            var d5 = new Darsena { NumeroDarsena = 5, Codigo = "Dársena 5", CapacidadMaximaPallets = 30, CapacidadMaximaM3 = 120m, EstadoActual = EstadoDarsena.Libre, DetalleOperacionActual = "Disponible ahora (Sin alquiler)" };
            var d6 = new Darsena { NumeroDarsena = 6, Codigo = "Dársena 6", CapacidadMaximaPallets = 30, CapacidadMaximaM3 = 120m, EstadoActual = EstadoDarsena.Libre, DetalleOperacionActual = "Disponible ahora (Sin alquiler)" };
            context.Darsenas.AddRange(d1, d2, d3, d4, d5, d6);
        }

        await context.SaveChangesAsync();
    }

    public static async Task ResetDatabaseToDemoAsync(LcpDbContext context)
    {
        await EnsureSchemaAsync(context);

        // Limpiar todas las tablas respetando la jerarquía de claves foráneas
        try
        {
            await context.Database.ExecuteSqlRawAsync(@"
                EXEC sp_MSforeachtable 'ALTER TABLE ? NOCHECK CONSTRAINT ALL';

                DELETE FROM [MensajesChat];
                DELETE FROM [DocumentosAdjuntos];
                DELETE FROM [TareasCalendario];
                DELETE FROM [Liquidaciones];
                DELETE FROM [Viajes];
                DELETE FROM [SolicitudesOperacion];
                DELETE FROM [LotesInventario];
                DELETE FROM [ContratosAlquilerDarsena];
                DELETE FROM [Conductores];
                DELETE FROM [Usuarios];
                DELETE FROM [ClientesEmpresa];
                DELETE FROM [Darsenas];
                DELETE FROM [TarifasConfiguracion];

                DBCC CHECKIDENT ('[ClientesEmpresa]', RESEED, 0);
                DBCC CHECKIDENT ('[Usuarios]', RESEED, 0);
                DBCC CHECKIDENT ('[Conductores]', RESEED, 0);
                DBCC CHECKIDENT ('[TarifasConfiguracion]', RESEED, 0);
                DBCC CHECKIDENT ('[Darsenas]', RESEED, 0);
                DBCC CHECKIDENT ('[ContratosAlquilerDarsena]', RESEED, 0);
                DBCC CHECKIDENT ('[SolicitudesOperacion]', RESEED, 0);
                DBCC CHECKIDENT ('[Viajes]', RESEED, 0);
                DBCC CHECKIDENT ('[LotesInventario]', RESEED, 0);
                DBCC CHECKIDENT ('[Liquidaciones]', RESEED, 0);
                DBCC CHECKIDENT ('[TareasCalendario]', RESEED, 0);
                DBCC CHECKIDENT ('[DocumentosAdjuntos]', RESEED, 0);
                DBCC CHECKIDENT ('[MensajesChat]', RESEED, 0);

                EXEC sp_MSforeachtable 'ALTER TABLE ? WITH CHECK CHECK CONSTRAINT ALL';
            ");
        }
        catch (Exception)
        {
            // Fallback EF Core si no es SQL Server
            context.MensajesChat.RemoveRange(context.MensajesChat);
            context.DocumentosAdjuntos.RemoveRange(context.DocumentosAdjuntos);
            context.TareasCalendario.RemoveRange(context.TareasCalendario);
            context.Liquidaciones.RemoveRange(context.Liquidaciones);
            context.Viajes.RemoveRange(context.Viajes);
            context.SolicitudesOperacion.RemoveRange(context.SolicitudesOperacion);
            context.LotesInventario.RemoveRange(context.LotesInventario);
            context.ContratosAlquilerDarsena.RemoveRange(context.ContratosAlquilerDarsena);
            context.Conductores.RemoveRange(context.Conductores);
            context.Usuarios.RemoveRange(context.Usuarios);
            context.ClientesEmpresa.RemoveRange(context.ClientesEmpresa);
            context.Darsenas.RemoveRange(context.Darsenas);
            context.TarifasConfiguracion.RemoveRange(context.TarifasConfiguracion);
            await context.SaveChangesAsync();
        }

        await SeedInternalAsync(context);
    }

    private static async Task SeedInternalAsync(LcpDbContext context)
    {
        // 1. Usuario Administrador (Único usuario inicial del sistema)
        var adminUser = new Usuario
        {
            Nombre = "Roberto",
            Apellido = "Gómez",
            Email = "admin@lcp.com",
            PasswordHash = HashPassword("Admin123!"),
            Telefono = "2954-600100",
            Rol = RolUsuario.Administrador,
            Activo = true,
            EmailConfirmado = true,
            FechaAlta = DateTime.UtcNow
        };

        context.Usuarios.Add(adminUser);
        await context.SaveChangesAsync();

        // 2. Tarifas de Configuración base del sistema
        var tarifas = new List<TarifaConfiguracion>
        {
            new() { CodigoConcepto = TarifaCodigos.AlquilerTrimestral, Descripcion = "Alquiler de Dársena Plan Trimestral (3 meses)", ValorUnitario = 450000m, ActualizadoPorUsuarioId = adminUser.Id },
            new() { CodigoConcepto = TarifaCodigos.AlquilerSemestral, Descripcion = "Alquiler de Dársena Plan Semestral (6 meses)", ValorUnitario = 820000m, ActualizadoPorUsuarioId = adminUser.Id },
            new() { CodigoConcepto = TarifaCodigos.AlquilerAnual, Descripcion = "Alquiler de Dársena Plan Anual (12 meses)", ValorUnitario = 1500000m, ActualizadoPorUsuarioId = adminUser.Id },
            new() { CodigoConcepto = TarifaCodigos.PrecioPorKg, Descripcion = "Tarifa Flota LCP por Kilogramo transportado", ValorUnitario = 50m, ActualizadoPorUsuarioId = adminUser.Id },
            new() { CodigoConcepto = TarifaCodigos.PrecioPorM3, Descripcion = "Tarifa Flota LCP por Metro Cúbico transportado", ValorUnitario = 8000m, ActualizadoPorUsuarioId = adminUser.Id },
            new() { CodigoConcepto = TarifaCodigos.PrecioPorKm, Descripcion = "Tarifa Flota LCP por Kilómetro recorrido", ValorUnitario = 1200m, ActualizadoPorUsuarioId = adminUser.Id },
            new() { CodigoConcepto = TarifaCodigos.CargoDescargaOperador, Descripcion = "Cargo operativo muelle descarga/carga y ordenamiento", ValorUnitario = 15000m, ActualizadoPorUsuarioId = adminUser.Id },
            new() { CodigoConcepto = TarifaCodigos.CargoDiaDeposito, Descripcion = "Cargo por estadía extraordinaria de acopio por día", ValorUnitario = 5000m, ActualizadoPorUsuarioId = adminUser.Id }
        };

        context.TarifasConfiguracion.AddRange(tarifas);
        await context.SaveChangesAsync();

        // 3. Dársenas 1 a 6 operativas del centro logístico (Disponibles y libres)
        var d1 = new Darsena { NumeroDarsena = 1, Codigo = "Dársena 1", CapacidadMaximaPallets = 32, CapacidadMaximaM3 = 140m, EstadoActual = EstadoDarsena.Libre, DetalleOperacionActual = "Disponible ahora (Sin alquiler)" };
        var d2 = new Darsena { NumeroDarsena = 2, Codigo = "Dársena 2", CapacidadMaximaPallets = 30, CapacidadMaximaM3 = 120m, EstadoActual = EstadoDarsena.Libre, DetalleOperacionActual = "Disponible ahora (Sin alquiler)" };
        var d3 = new Darsena { NumeroDarsena = 3, Codigo = "Dársena 3", CapacidadMaximaPallets = 28, CapacidadMaximaM3 = 110m, EstadoActual = EstadoDarsena.Libre, DetalleOperacionActual = "Disponible ahora (Sin alquiler)" };
        var d4 = new Darsena { NumeroDarsena = 4, Codigo = "Dársena 4", CapacidadMaximaPallets = 36, CapacidadMaximaM3 = 150m, EstadoActual = EstadoDarsena.Libre, DetalleOperacionActual = "Disponible ahora (Sin alquiler)" };
        var d5 = new Darsena { NumeroDarsena = 5, Codigo = "Dársena 5", CapacidadMaximaPallets = 30, CapacidadMaximaM3 = 120m, EstadoActual = EstadoDarsena.Libre, DetalleOperacionActual = "Disponible ahora (Sin alquiler)" };
        var d6 = new Darsena { NumeroDarsena = 6, Codigo = "Dársena 6", CapacidadMaximaPallets = 30, CapacidadMaximaM3 = 120m, EstadoActual = EstadoDarsena.Libre, DetalleOperacionActual = "Disponible ahora (Sin alquiler)" };

        context.Darsenas.AddRange(d1, d2, d3, d4, d5, d6);
        await context.SaveChangesAsync();
    }

    public static string HashPassword(string password)
    {
        using var sha256 = SHA256.Create();
        var hashedBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password + "_LCP_SALT_2026"));
        return Convert.ToBase64String(hashedBytes);
    }

    public static bool VerifyPassword(string password, string storedHash)
    {
        return HashPassword(password) == storedHash;
    }

    public static string GenerarPasswordProvisoria(string prefijo = "LCP")
    {
        const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        var rnd = new Random();
        var code = new string(Enumerable.Repeat(chars, 6).Select(s => s[rnd.Next(s.Length)]).ToArray());
        return $"{prefijo}#{code}!";
    }
}
