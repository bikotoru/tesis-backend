using System.Security.Claims;
using System.Text.Json;
using Backend.Datos;
using Backend.Modelos;
using Backend.Servicios;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace Pruebas;

public static class Ayudas
{
    public const string ClaveDePrueba = "clave1234";
    public const string SecretoApp = "secreto-de-prueba";

    static readonly string HashDePrueba = new PasswordHasher<Usuario>().HashPassword(new Usuario(), ClaveDePrueba);

    public static AppDbContext NuevaBase()
    {
        var opciones = new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        var db = new AppDbContext(opciones);

        db.Roles.Add(new Rol { RolId = 1, Nombre = "Administrador" });
        db.Roles.Add(new Rol { RolId = 2, Nombre = "Vendedor" });
        db.Usuarios.Add(new Usuario { UsuarioId = 1, RolId = 1, Nombre = "Admin", Correo = "admin@ferrelupe.cl", ClaveHash = HashDePrueba });
        db.Usuarios.Add(new Usuario { UsuarioId = 2, RolId = 2, Nombre = "Vendedora", Correo = "venta@ferrelupe.cl", ClaveHash = HashDePrueba });
        db.Configuracion.Add(new Configuracion { MensajeBienvenida = "Hola", Horario = "Lunes a sábado", RendimientoPintura = 10, MargenMedidas = 10 });
        db.Categorias.Add(new Categoria { CategoriaId = 1, Nombre = "Pinturas" });
        db.Productos.Add(new Producto { ProductoId = 1, CategoriaId = 1, Codigo = "PIN-001", Nombre = "Látex blanco", Unidad = "galón", Precio = 14990, Stock = 10 });
        db.Productos.Add(new Producto { ProductoId = 2, CategoriaId = 1, Codigo = "PIN-002", Nombre = "Rodillo", Unidad = "unidad", Precio = 3990, Stock = 5 });
        db.Productos.Add(new Producto { ProductoId = 3, CategoriaId = 1, Codigo = "PIN-003", Nombre = "Esmalte antiguo", Unidad = "galón", Precio = 9990, Stock = 5, Activo = false });
        db.Clientes.Add(new Cliente { ClienteId = 1, Telefono = "56911111111", Nombre = "Ana" });
        db.Conversaciones.Add(new Conversacion { ConversacionId = 1, ClienteId = 1, Estado = "Asistente" });
        db.Mensajes.Add(new Mensaje { MensajeId = 1, ConversacionId = 1, Emisor = "Cliente", Texto = "Hola", IdWhatsApp = "wamid.1" });
        db.SaveChanges();
        return db;
    }

    public static IConfiguration Configuracion()
    {
        var valores = new Dictionary<string, string?>();
        valores["WhatsApp:Simulado"] = "true";
        valores["WhatsApp:SecretoApp"] = SecretoApp;
        valores["WhatsApp:TokenVerificacion"] = "token-de-prueba";
        valores["Ia:Simulada"] = "true";
        valores["Ia:EsperaSegundos"] = "0";
        valores["Erp:Simulado"] = "true";
        valores["Jwt:Clave"] = "una-clave-de-prueba-de-al-menos-32-caracteres";
        return new ConfigurationBuilder().AddInMemoryCollection(valores).Build();
    }

    public static Mensajeria NuevaMensajeria(AppDbContext db)
    {
        var whatsApp = new WhatsApp(new HttpClient(), Configuracion(), NullLogger<WhatsApp>.Instance);
        return new Mensajeria(db, whatsApp);
    }

    public static Asistente NuevoAsistente(AppDbContext db)
    {
        var whatsApp = new WhatsApp(new HttpClient(), Configuracion(), NullLogger<WhatsApp>.Instance);
        return new Asistente(db, NuevaMensajeria(db), whatsApp, Configuracion(), NullLogger<Asistente>.Instance);
    }

    public static HerramientasNormales NuevasHerramientas(AppDbContext db)
    {
        return new HerramientasNormales(db, NuevaMensajeria(db), NullLogger.Instance);
    }

    public static ClaimsPrincipal Usuario(int id, string rol)
    {
        var datos = new List<Claim>();
        datos.Add(new Claim(ClaimTypes.NameIdentifier, id.ToString()));
        datos.Add(new Claim(ClaimTypes.Role, rol));
        return new ClaimsPrincipal(new ClaimsIdentity(datos, "Prueba"));
    }

    public static T ConUsuario<T>(T controlador, int id, string rol) where T : ControllerBase
    {
        var contexto = new DefaultHttpContext();
        contexto.User = Usuario(id, rol);
        controlador.ControllerContext = new ControllerContext { HttpContext = contexto };
        return controlador;
    }

    public static int Estado(IActionResult resultado)
    {
        var conEstado = (IStatusCodeActionResult)resultado;
        return conEstado.StatusCode ?? 200;
    }

    public static JsonElement Datos(IActionResult resultado)
    {
        var conDatos = (ObjectResult)resultado;
        return JsonSerializer.SerializeToElement(conDatos.Value);
    }

    public static string Mensaje(IActionResult resultado)
    {
        return Datos(resultado).GetProperty("mensaje").GetString() ?? "";
    }

    public static void AgregarMensajeDelCliente(AppDbContext db, int id, int conversacionId, DateTime fecha)
    {
        db.Mensajes.Add(new Mensaje { MensajeId = id, ConversacionId = conversacionId, Emisor = "Cliente", Texto = "Otro mensaje", Fecha = fecha });
        db.SaveChanges();
    }
}
