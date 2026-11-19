using Backend.Controllers;
using Backend.Datos;
using Backend.Modelos;
using Backend.Servicios;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Pruebas;

public class ManejadorQueFalla : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        throw new InvalidOperationException("No se debía llamar a WhatsApp.");
    }
}

public class SimuladorPruebas
{
    static ConversacionesController NuevoControlador(AppDbContext db)
    {
        var fabrica = new ServiceCollection().BuildServiceProvider().GetRequiredService<IServiceScopeFactory>();
        var controlador = new ConversacionesController(db, Ayudas.NuevaMensajeria(db), fabrica, NullLogger<ConversacionesController>.Instance);
        return Ayudas.ConUsuario(controlador, 1, "Administrador");
    }

    static int CrearConversacionDePrueba(AppDbContext db)
    {
        var cliente = new Cliente { Telefono = "PRUEBA-1", Nombre = "Prueba", EsPrueba = true };
        var conversacion = new Conversacion { Cliente = cliente, Estado = "Asistente" };
        db.Conversaciones.Add(conversacion);
        db.SaveChanges();
        db.Mensajes.Add(new Mensaje { ConversacionId = conversacion.ConversacionId, Emisor = "Cliente", Texto = "Hola" });
        db.SaveChanges();
        return conversacion.ConversacionId;
    }

    [Fact]
    public async Task Simular_CreaUnClienteDePruebaConSuPrimerMensaje()
    {
        var db = Ayudas.NuevaBase();

        var resultado = await NuevoControlador(db).Simular(new DatosSimulacion { Nombre = "Docente", Texto = "Quiero pintar mi pieza" });

        int id = Ayudas.Datos(resultado).GetProperty("id").GetInt32();
        var conversacion = db.Conversaciones.Find(id)!;
        var cliente = db.Clientes.Find(conversacion.ClienteId)!;
        Assert.True(cliente.EsPrueba);
        Assert.Equal("Docente", cliente.Nombre);
        Assert.StartsWith("PRUEBA-", cliente.Telefono);
        Assert.Single(db.Mensajes.Where(m => m.ConversacionId == id && m.Emisor == "Cliente"));
    }

    [Fact]
    public async Task Simular_SinNombre_UsaUnNombrePorDefecto()
    {
        var db = Ayudas.NuevaBase();

        var resultado = await NuevoControlador(db).Simular(new DatosSimulacion { Texto = "Hola" });

        int id = Ayudas.Datos(resultado).GetProperty("id").GetInt32();
        var conversacion = db.Conversaciones.Find(id)!;
        Assert.Equal("Cliente de prueba", db.Clientes.Find(conversacion.ClienteId)!.Nombre);
    }

    [Fact]
    public async Task Simular_SinTexto_Devuelve400()
    {
        var db = Ayudas.NuevaBase();

        var resultado = await NuevoControlador(db).Simular(new DatosSimulacion { Texto = "  " });

        Assert.Equal(400, Ayudas.Estado(resultado));
    }

    [Fact]
    public async Task Simular_NombreMuyLargo_Devuelve400()
    {
        var db = Ayudas.NuevaBase();

        var resultado = await NuevoControlador(db).Simular(new DatosSimulacion { Nombre = new string('a', 101), Texto = "Hola" });

        Assert.Equal(400, Ayudas.Estado(resultado));
    }

    [Fact]
    public async Task SimularMensaje_AgregaUnMensajeDelCliente()
    {
        var db = Ayudas.NuevaBase();
        int id = CrearConversacionDePrueba(db);

        var resultado = await NuevoControlador(db).SimularMensaje(id, new DatosRespuesta { Texto = "Son 3 por 4 metros" });

        Assert.Equal(200, Ayudas.Estado(resultado));
        Assert.Equal(2, db.Mensajes.Count(m => m.ConversacionId == id && m.Emisor == "Cliente"));
    }

    [Fact]
    public async Task SimularMensaje_EnConversacionReal_Devuelve400()
    {
        var db = Ayudas.NuevaBase();

        var resultado = await NuevoControlador(db).SimularMensaje(1, new DatosRespuesta { Texto = "Hola" });

        Assert.Equal(400, Ayudas.Estado(resultado));
    }

    [Fact]
    public async Task SimularMensaje_ConversacionInexistente_Devuelve404()
    {
        var db = Ayudas.NuevaBase();

        var resultado = await NuevoControlador(db).SimularMensaje(99, new DatosRespuesta { Texto = "Hola" });

        Assert.Equal(404, Ayudas.Estado(resultado));
    }

    [Fact]
    public async Task Obtener_IndicaSiLaConversacionEsDePrueba()
    {
        var db = Ayudas.NuevaBase();
        int id = CrearConversacionDePrueba(db);

        var dePrueba = await NuevoControlador(db).Obtener(id);
        var real = await NuevoControlador(db).Obtener(1);

        Assert.True(Ayudas.Datos(dePrueba).GetProperty("prueba").GetBoolean());
        Assert.False(Ayudas.Datos(real).GetProperty("prueba").GetBoolean());
    }

    [Fact]
    public async Task Enviar_AClienteDePrueba_NoUsaWhatsAppYGuardaElMensaje()
    {
        var db = Ayudas.NuevaBase();
        int id = CrearConversacionDePrueba(db);
        var valores = new Dictionary<string, string?>();
        valores["WhatsApp:Simulado"] = "false";
        var configuracion = new ConfigurationBuilder().AddInMemoryCollection(valores).Build();
        var whatsApp = new WhatsApp(new HttpClient(new ManejadorQueFalla()), configuracion, NullLogger<WhatsApp>.Instance);
        var mensajeria = new Mensajeria(db, whatsApp);

        string? aviso = await mensajeria.Enviar(id, "Respuesta del asistente", "Asistente");

        Assert.Null(aviso);
        Assert.Single(db.Mensajes.Where(m => m.ConversacionId == id && m.Emisor == "Asistente"));
    }
}
