using Microsoft.EntityFrameworkCore;
using LCP.Api.Data.Entities;

namespace LCP.Api.Data;

public class LcpDbContext : DbContext
{
    public LcpDbContext(DbContextOptions<LcpDbContext> options) : base(options)
    {
    }

    public DbSet<Usuario> Usuarios => Set<Usuario>();
    public DbSet<ClienteEmpresa> ClientesEmpresa => Set<ClienteEmpresa>();
    public DbSet<Conductor> Conductores => Set<Conductor>();
    public DbSet<TarifaConfiguracion> TarifasConfiguracion => Set<TarifaConfiguracion>();
    public DbSet<Darsena> Darsenas => Set<Darsena>();
    public DbSet<ContratoAlquilerDarsena> ContratosAlquilerDarsena => Set<ContratoAlquilerDarsena>();
    public DbSet<SolicitudOperacion> SolicitudesOperacion => Set<SolicitudOperacion>();
    public DbSet<Viaje> Viajes => Set<Viaje>();
    public DbSet<LoteInventario> LotesInventario => Set<LoteInventario>();
    public DbSet<Liquidacion> Liquidaciones => Set<Liquidacion>();
    public DbSet<TareaCalendario> TareasCalendario => Set<TareaCalendario>();
    public DbSet<DocumentoAdjunto> DocumentosAdjuntos => Set<DocumentoAdjunto>();
    public DbSet<MensajeChat> MensajesChat => Set<MensajeChat>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Unique constraints
        modelBuilder.Entity<Usuario>()
            .HasIndex(u => u.Email)
            .IsUnique();

        modelBuilder.Entity<ClienteEmpresa>()
            .HasIndex(c => c.CUIT)
            .IsUnique();

        modelBuilder.Entity<Conductor>()
            .HasIndex(c => c.CUIL)
            .IsUnique();

        modelBuilder.Entity<TarifaConfiguracion>()
            .HasIndex(t => t.CodigoConcepto)
            .IsUnique();

        // 1:1 SolicitudOperacion <-> Viaje
        modelBuilder.Entity<SolicitudOperacion>()
            .HasOne(s => s.Viaje)
            .WithOne(v => v.SolicitudOperacion)
            .HasForeignKey<Viaje>(v => v.SolicitudOperacionId)
            .OnDelete(DeleteBehavior.Cascade);

        // Decimals precision
        foreach (var property in modelBuilder.Model.GetEntityTypes()
            .SelectMany(t => t.GetProperties())
            .Where(p => p.ClrType == typeof(decimal) || p.ClrType == typeof(decimal?)))
        {
            property.SetPrecision(18);
            property.SetScale(2);
        }
    }
}
