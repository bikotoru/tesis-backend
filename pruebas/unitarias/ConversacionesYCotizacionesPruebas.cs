using Backend.Controllers;
using Backend.Datos;
using Backend.Servicios;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Pruebas;

public class ConversacionesYCotizacionesPruebas
{
    static ConversacionesController NuevoConversaciones(AppDbContext db)
    {
        var fabrica = new ServiceCollection().BuildServiceProvider().GetRequiredService<IServiceScopeFactory>();
        var controlador = new ConversacionesController(db, Ayudas.NuevaMensajeria(db), fabrica, NullLogger<ConversacionesController>.Instance);
        return Ayudas.ConUsuario(controlador, 2, "Vendedor");
    }

    static CotizacionesController NuevoCotizaciones(AppDbContext db)
    {
        var controlador = new CotizacionesController(db, Ayudas.NuevaMensajeria(db));
        return Ayudas.ConUsuario(controlador, 2, "Vendedor");
    }

    static DatosCotizacion CotizacionValida()
    {
        var datos = new DatosCotizacion();
        datos.ClienteId = 1;
        datos.Lineas = new List<Linea> { new Linea { ProductoId = 1, Cantidad = 2 } };
        return datos;
    }

    [Fact]
    public async Task Listar_MuestraElUltimoMensajeYLaVentana()
    {
        var db = Ayudas.NuevaBase();

        var resultado = await NuevoConversaciones(db).Listar();

        var primera = Ayudas.Datos(resultado)[0];
        Assert.Equal("Hola", primera.GetProperty("ultimo").GetProperty("Texto").GetString());
        Assert.True(primera.GetProperty("ventanaAbierta").GetBoolean());
    }

    [Fact]
    public async Task Obtener_DevuelveLosMensajes()
    {
        var db = Ayudas.NuevaBase();

        var resultado = await NuevoConversaciones(db).Obtener(1);

        Assert.Equal(1, Ayudas.Datos(resultado).GetProperty("mensajes").GetArrayLength());
    }

    [Fact]
    public async Task Obtener_ConversacionInexistente_Devuelve404()
    {
        var db = Ayudas.NuevaBase();

        var resultado = await NuevoConversaciones(db).Obtener(99);

        Assert.Equal(404, Ayudas.Estado(resultado));
    }

    [Fact]
    public async Task Tomar_PasaLaConversacionAlVendedor()
    {
        var db = Ayudas.NuevaBase();

        await NuevoConversaciones(db).Tomar(1);

        Assert.Equal("Vendedor", db.Conversaciones.Find(1)!.Estado);
        Assert.Equal(2, db.Conversaciones.Find(1)!.UsuarioId);
    }

    [Fact]
    public async Task Devolver_LaConversacionVuelveAlAsistente()
    {
        var db = Ayudas.NuevaBase();
        await NuevoConversaciones(db).Tomar(1);

        await NuevoConversaciones(db).Devolver(1);

        Assert.Equal("Asistente", db.Conversaciones.Find(1)!.Estado);
    }

    [Fact]
    public async Task Responder_SinTomarLaConversacion_DevuelveError()
    {
        var db = Ayudas.NuevaBase();

        var resultado = await NuevoConversaciones(db).Responder(1, new DatosRespuesta { Texto = "Hola" });

        Assert.Equal(400, Ayudas.Estado(resultado));
    }

    [Fact]
    public async Task Responder_ConversacionTomada_GuardaElMensaje()
    {
        var db = Ayudas.NuevaBase();
        await NuevoConversaciones(db).Tomar(1);

        var resultado = await NuevoConversaciones(db).Responder(1, new DatosRespuesta { Texto = " Hola, Ana " });

        Assert.Equal(200, Ayudas.Estado(resultado));
        Assert.Contains(db.Mensajes, m => m.Emisor == "Vendedor" && m.Texto == "Hola, Ana");
    }

    [Fact]
    public async Task Responder_ConVentanaCerrada_AvisaQueNoSePuedeEnviar()
    {
        var db = Ayudas.NuevaBase();
        db.Mensajes.Find(1)!.Fecha = DateTime.UtcNow.AddHours(-25);
        db.SaveChanges();
        await NuevoConversaciones(db).Tomar(1);

        var resultado = await NuevoConversaciones(db).Responder(1, new DatosRespuesta { Texto = "Hola" });

        Assert.Contains("hasta que el cliente envíe un mensaje nuevo", Ayudas.Mensaje(resultado));
    }

    [Fact]
    public async Task Responder_SinTexto_PideUnMensaje()
    {
        var db = Ayudas.NuevaBase();

        var resultado = await NuevoConversaciones(db).Responder(1, new DatosRespuesta());

        Assert.Equal("Escribe un mensaje o adjunta una imagen.", Ayudas.Mensaje(resultado));
    }

    [Fact]
    public async Task CrearCotizacion_GuardaYLaEnviaAlCliente()
    {
        var db = Ayudas.NuevaBase();

        var resultado = await NuevoCotizaciones(db).Crear(CotizacionValida());

        Assert.Equal(200, Ayudas.Estado(resultado));
        Assert.Equal(29980, db.Cotizaciones.Single().Total);
        Assert.Contains(db.Mensajes, m => m.Texto.StartsWith("Se envió en PDF: Cotización N.º"));
    }

    [Fact]
    public async Task CrearCotizacion_ConClienteInexistente_DevuelveError()
    {
        var db = Ayudas.NuevaBase();
        var datos = CotizacionValida();
        datos.ClienteId = 99;

        var resultado = await NuevoCotizaciones(db).Crear(datos);

        Assert.Equal("El cliente no existe.", Ayudas.Mensaje(resultado));
    }

    [Fact]
    public async Task CrearCotizacion_SinProductos_DevuelveError()
    {
        var db = Ayudas.NuevaBase();
        var datos = CotizacionValida();
        datos.Lineas = null;

        var resultado = await NuevoCotizaciones(db).Crear(datos);

        Assert.Equal("Agrega al menos un producto.", Ayudas.Mensaje(resultado));
    }

    [Fact]
    public async Task ListarYObtenerCotizacion_MuestranElTotal()
    {
        var db = Ayudas.NuevaBase();
        await NuevoCotizaciones(db).Crear(CotizacionValida());
        int id = db.Cotizaciones.Single().CotizacionId;

        var lista = await NuevoCotizaciones(db).Listar();
        var detalle = await NuevoCotizaciones(db).Obtener(id);

        Assert.Equal(29980, Ayudas.Datos(lista)[0].GetProperty("total").GetInt32());
        Assert.Equal(29980, Ayudas.Datos(detalle).GetProperty("total").GetInt32());
    }

    [Fact]
    public async Task AjustarCotizacion_ReemplazaLosProductos()
    {
        var db = Ayudas.NuevaBase();
        await NuevoCotizaciones(db).Crear(CotizacionValida());
        int id = db.Cotizaciones.Single().CotizacionId;
        var datos = CotizacionValida();
        datos.Lineas = new List<Linea> { new Linea { ProductoId = 2, Cantidad = 1 } };

        await NuevoCotizaciones(db).Ajustar(id, datos);

        Assert.Equal(3990, db.Cotizaciones.Single().Total);
    }

    [Fact]
    public async Task CambiarEstado_ConEstadoValido_LoGuarda()
    {
        var db = Ayudas.NuevaBase();
        await NuevoCotizaciones(db).Crear(CotizacionValida());
        int id = db.Cotizaciones.Single().CotizacionId;

        await NuevoCotizaciones(db).CambiarEstado(id, new DatosEstado { Estado = "Aceptada" });

        Assert.Equal("Aceptada", db.Cotizaciones.Single().Estado);
    }

    [Fact]
    public async Task CambiarEstado_ConEstadoInvalido_DevuelveError()
    {
        var db = Ayudas.NuevaBase();

        var resultado = await NuevoCotizaciones(db).CambiarEstado(1, new DatosEstado { Estado = "Pagada" });

        Assert.Equal("El estado no es válido.", Ayudas.Mensaje(resultado));
    }

    [Fact]
    public async Task Reenviar_CotizacionExistente_LaEnviaDeNuevo()
    {
        var db = Ayudas.NuevaBase();
        await NuevoCotizaciones(db).Crear(CotizacionValida());
        int id = db.Cotizaciones.Single().CotizacionId;

        var resultado = await NuevoCotizaciones(db).Reenviar(id);

        Assert.Equal("Cotización reenviada por WhatsApp.", Ayudas.Mensaje(resultado));
    }

    [Fact]
    public async Task Reenviar_CotizacionInexistente_Devuelve404()
    {
        var db = Ayudas.NuevaBase();

        var resultado = await NuevoCotizaciones(db).Reenviar(99);

        Assert.Equal(404, Ayudas.Estado(resultado));
    }
}
