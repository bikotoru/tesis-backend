using Backend.Modelos;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Backend.Datos;

public class AppDbContext(DbContextOptions<AppDbContext> opciones) : DbContext(opciones)
{
    public DbSet<Rol> Roles { get { return Set<Rol>(); } }
    public DbSet<Usuario> Usuarios { get { return Set<Usuario>(); } }
    public DbSet<Cliente> Clientes { get { return Set<Cliente>(); } }
    public DbSet<Categoria> Categorias { get { return Set<Categoria>(); } }
    public DbSet<Producto> Productos { get { return Set<Producto>(); } }
    public DbSet<Conversacion> Conversaciones { get { return Set<Conversacion>(); } }
    public DbSet<Mensaje> Mensajes { get { return Set<Mensaje>(); } }
    public DbSet<Cotizacion> Cotizaciones { get { return Set<Cotizacion>(); } }
    public DbSet<DetalleCotizacion> DetallesCotizacion { get { return Set<DetalleCotizacion>(); } }
    public DbSet<Configuracion> Configuracion { get { return Set<Configuracion>(); } }
    public DbSet<Adjunto> Adjuntos { get { return Set<Adjunto>(); } }

    protected override void ConfigureConventions(ModelConfigurationBuilder convenciones)
    {
        convenciones.Properties<DateTime>().HaveConversion<FechaUtc>();
    }

    protected override void OnModelCreating(ModelBuilder modelo)
    {
        modelo.Entity<Rol>().ToTable("Roles");
        modelo.Entity<Usuario>().ToTable("Usuarios");
        modelo.Entity<Cliente>().ToTable("Clientes");
        modelo.Entity<Categoria>().ToTable("Categorias");
        modelo.Entity<Producto>().ToTable("Productos");
        modelo.Entity<Conversacion>().ToTable("Conversaciones");
        modelo.Entity<Mensaje>().ToTable("Mensajes");
        modelo.Entity<Cotizacion>().ToTable("Cotizaciones");
        modelo.Entity<DetalleCotizacion>().ToTable("DetallesCotizacion").HasKey(d => new { d.CotizacionId, d.ProductoId });
        modelo.Entity<Configuracion>().ToTable("Configuracion").Property(c => c.ConfiguracionId).ValueGeneratedNever();
        modelo.Entity<Configuracion>().Property(c => c.RendimientoPintura).HasPrecision(4, 1);
        modelo.Entity<Producto>().Property(p => p.Litros).HasPrecision(6, 3);
        modelo.Entity<Adjunto>().ToTable("Adjuntos");
    }
}

public class FechaUtc : ValueConverter<DateTime, DateTime>
{
    public FechaUtc() : base(fecha => fecha, fecha => DateTime.SpecifyKind(fecha, DateTimeKind.Utc))
    {
    }
}
