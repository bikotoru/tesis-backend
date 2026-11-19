using Backend.Controllers;
using Backend.Datos;
using Backend.Modelos;
using Backend.Servicios;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Pruebas;

public class AdjuntosPruebas
{
    static readonly string ImagenDePrueba = Convert.ToBase64String([1, 2, 3, 4]);

    static ConversacionesController NuevoControlador(AppDbContext db, int usuario, string rol)
    {
        var fabrica = new ServiceCollection().BuildServiceProvider().GetRequiredService<IServiceScopeFactory>();
        var controlador = new ConversacionesController(db, Ayudas.NuevaMensajeria(db), fabrica, NullLogger<ConversacionesController>.Instance);
        return Ayudas.ConUsuario(controlador, usuario, rol);
    }

    static WhatsApp NuevoWhatsApp()
    {
        return new WhatsApp(new HttpClient(), Ayudas.Configuracion(), NullLogger<WhatsApp>.Instance);
    }

    [Fact]
    public async Task Simular_ConImagen_GuardaElAdjunto()
    {
        var db = Ayudas.NuevaBase();
        var datos = new DatosSimulacion { Texto = "Así es el muro", Imagen = new DatosImagen { Tipo = "image/jpeg", Datos = ImagenDePrueba } };

        var resultado = await NuevoControlador(db, 1, "Administrador").Simular(datos);

        int id = Ayudas.Datos(resultado).GetProperty("id").GetInt32();
        var mensaje = db.Mensajes.Single(m => m.ConversacionId == id);
        Assert.Equal("[Imagen] Así es el muro", mensaje.Texto);
        Assert.Equal(4, db.Adjuntos.Single(a => a.MensajeId == mensaje.MensajeId).Datos.Length);
    }

    [Fact]
    public async Task Simular_SoloImagen_UsaElTextoImagen()
    {
        var db = Ayudas.NuevaBase();
        var datos = new DatosSimulacion { Texto = "", Imagen = new DatosImagen { Tipo = "image/png", Datos = ImagenDePrueba } };

        var resultado = await NuevoControlador(db, 1, "Administrador").Simular(datos);

        int id = Ayudas.Datos(resultado).GetProperty("id").GetInt32();
        Assert.Equal("[Imagen]", db.Mensajes.Single(m => m.ConversacionId == id).Texto);
    }

    [Fact]
    public async Task Simular_ImagenDeOtroTipo_Devuelve400()
    {
        var db = Ayudas.NuevaBase();
        var datos = new DatosSimulacion { Texto = "Hola", Imagen = new DatosImagen { Tipo = "image/gif", Datos = ImagenDePrueba } };

        var resultado = await NuevoControlador(db, 1, "Administrador").Simular(datos);

        Assert.Equal(400, Ayudas.Estado(resultado));
    }

    [Fact]
    public async Task Simular_ImagenMalCodificada_Devuelve400()
    {
        var db = Ayudas.NuevaBase();
        var datos = new DatosSimulacion { Texto = "Hola", Imagen = new DatosImagen { Tipo = "image/png", Datos = "no es base64" } };

        var resultado = await NuevoControlador(db, 1, "Administrador").Simular(datos);

        Assert.Equal(400, Ayudas.Estado(resultado));
    }

    [Fact]
    public async Task Responder_ConImagen_GuardaElAdjunto()
    {
        var db = Ayudas.NuevaBase();
        var controlador = NuevoControlador(db, 2, "Vendedor");
        await controlador.Tomar(1);
        var datos = new DatosRespuesta { Texto = "Este es el producto", Imagen = new DatosImagen { Tipo = "image/jpeg", Datos = ImagenDePrueba } };

        var resultado = await controlador.Responder(1, datos);

        Assert.Equal(200, Ayudas.Estado(resultado));
        var mensaje = db.Mensajes.Single(m => m.Emisor == "Vendedor");
        Assert.Equal("[Imagen] Este es el producto", mensaje.Texto);
        Assert.Single(db.Adjuntos.Where(a => a.MensajeId == mensaje.MensajeId));
    }

    [Fact]
    public async Task Imagen_DevuelveElArchivoGuardado()
    {
        var db = Ayudas.NuevaBase();
        db.Adjuntos.Add(new Adjunto { MensajeId = 1, Tipo = "image/png", Datos = [9, 9] });
        db.SaveChanges();

        var resultado = await NuevoControlador(db, 2, "Vendedor").Imagen(1, 1, NuevoWhatsApp());

        var archivo = Assert.IsType<FileContentResult>(resultado);
        Assert.Equal("image/png", archivo.ContentType);
        Assert.Equal("imagen-1.png", archivo.FileDownloadName);
    }

    [Fact]
    public async Task Imagen_MensajeSinImagen_Devuelve404()
    {
        var db = Ayudas.NuevaBase();

        var resultado = await NuevoControlador(db, 2, "Vendedor").Imagen(1, 1, NuevoWhatsApp());

        Assert.Equal(404, Ayudas.Estado(resultado));
    }

    [Fact]
    public async Task Obtener_IndicaQueMensajeTieneImagen()
    {
        var db = Ayudas.NuevaBase();
        db.Adjuntos.Add(new Adjunto { MensajeId = 1, Tipo = "image/png", Datos = [9] });
        db.SaveChanges();

        var resultado = await NuevoControlador(db, 2, "Vendedor").Obtener(1);

        var mensaje = Ayudas.Datos(resultado).GetProperty("mensajes")[0];
        Assert.True(mensaje.GetProperty("tieneImagen").GetBoolean());
    }

    [Fact]
    public async Task Cotizacion_EnviadaQuedaUnidaAlMensajeYSeDescargaEnPdf()
    {
        var db = Ayudas.NuevaBase();
        var cotizaciones = Ayudas.ConUsuario(new CotizacionesController(db, Ayudas.NuevaMensajeria(db)), 2, "Vendedor");
        var datos = new DatosCotizacion { ClienteId = 1, Lineas = [new Linea { ProductoId = 1, Cantidad = 2 }] };
        await cotizaciones.Crear(datos);
        var cotizacion = db.Cotizaciones.Single();

        var resultado = await cotizaciones.Pdf(cotizacion.CotizacionId);

        Assert.Single(db.Mensajes.Where(m => m.CotizacionId == cotizacion.CotizacionId));
        var archivo = Assert.IsType<FileContentResult>(resultado);
        Assert.Equal("application/pdf", archivo.ContentType);
    }

    [Fact]
    public async Task Pdf_CotizacionInexistente_Devuelve404()
    {
        var db = Ayudas.NuevaBase();
        var cotizaciones = Ayudas.ConUsuario(new CotizacionesController(db, Ayudas.NuevaMensajeria(db)), 2, "Vendedor");

        var resultado = await cotizaciones.Pdf(9999);

        Assert.Equal(404, Ayudas.Estado(resultado));
    }
}
