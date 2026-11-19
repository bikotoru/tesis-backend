using System.Text;
using Backend.Controllers;
using Backend.Datos;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Pruebas;

public class WebhookPruebas
{
    static WhatsAppController NuevoWebhook(AppDbContext db, string cuerpo, string firma)
    {
        var fabrica = new ServiceCollection().BuildServiceProvider().GetRequiredService<IServiceScopeFactory>();
        var controlador = new WhatsAppController(db, Ayudas.Configuracion(), fabrica, NullLogger<WhatsAppController>.Instance);
        var contexto = new DefaultHttpContext();
        contexto.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(cuerpo));
        contexto.Request.Headers["X-Hub-Signature-256"] = firma;
        controlador.ControllerContext = new ControllerContext { HttpContext = contexto };
        return controlador;
    }

    static string Aviso(string idWhatsApp, string telefono, string timestamp)
    {
        return "{\"entry\":[{\"changes\":[{\"value\":{" +
               "\"contacts\":[{\"wa_id\":\"" + telefono + "\",\"profile\":{\"name\":\"Luis\"}}]," +
               "\"messages\":[{\"id\":\"" + idWhatsApp + "\",\"from\":\"" + telefono + "\",\"timestamp\":\"" + timestamp + "\"," +
               "\"type\":\"text\",\"text\":{\"body\":\"Necesito pintura\"}}]}}]}]}";
    }

    static async Task<IActionResult> Recibir(AppDbContext db, string cuerpo)
    {
        string firma = WhatsAppPruebas.Firmar(Encoding.UTF8.GetBytes(cuerpo), Ayudas.SecretoApp);
        var resultado = await NuevoWebhook(db, cuerpo, firma).Recibir();
        return resultado;
    }

    [Fact]
    public void Verificar_ConElTokenCorrecto_DevuelveElDesafio()
    {
        var db = Ayudas.NuevaBase();

        var resultado = NuevoWebhook(db, "", "").Verificar("subscribe", "token-de-prueba", "12345");

        Assert.Equal("12345", ((ContentResult)resultado).Content);
    }

    [Fact]
    public void Verificar_ConOtroToken_Devuelve403()
    {
        var db = Ayudas.NuevaBase();

        var resultado = NuevoWebhook(db, "", "").Verificar("subscribe", "otro", "12345");

        Assert.Equal(403, Ayudas.Estado(resultado));
    }

    [Fact]
    public async Task Recibir_ConFirmaInvalida_Devuelve401()
    {
        var db = Ayudas.NuevaBase();
        string cuerpo = Aviso("wamid.2", "56922222222", "1700000000");

        var resultado = await NuevoWebhook(db, cuerpo, "sha256=00").Recibir();

        Assert.Equal(401, Ayudas.Estado(resultado));
        Assert.Equal(1, db.Mensajes.Count());
    }

    [Fact]
    public async Task Recibir_ClienteNuevo_GuardaClienteConversacionYMensaje()
    {
        var db = Ayudas.NuevaBase();
        string timestamp = DateTimeOffset.UtcNow.AddMinutes(-2).ToUnixTimeSeconds().ToString();

        var resultado = await Recibir(db, Aviso("wamid.2", "56922222222", timestamp));

        Assert.Equal(200, Ayudas.Estado(resultado));
        var cliente = db.Clientes.Single(c => c.Telefono == "56922222222");
        Assert.Equal("Luis", cliente.Nombre);
        var mensaje = db.Mensajes.Single(m => m.IdWhatsApp == "wamid.2");
        Assert.Equal(long.Parse(timestamp), new DateTimeOffset(mensaje.Fecha).ToUnixTimeSeconds());
    }

    [Fact]
    public async Task Recibir_AvisoRepetido_SeGuardaUnaSolaVez()
    {
        var db = Ayudas.NuevaBase();
        string cuerpo = Aviso("wamid.2", "56922222222", "1700000000");

        await Recibir(db, cuerpo);
        await Recibir(db, cuerpo);

        Assert.Equal(1, db.Mensajes.Count(m => m.IdWhatsApp == "wamid.2"));
    }

    [Fact]
    public async Task Recibir_DentroDeLaVentana_UsaLaMismaConversacion()
    {
        var db = Ayudas.NuevaBase();
        string timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();

        await Recibir(db, Aviso("wamid.2", "56911111111", timestamp));

        Assert.Equal(1, db.Conversaciones.Count());
    }

    [Fact]
    public async Task Recibir_ConLaVentanaCerrada_EmpiezaOtraConversacion()
    {
        var db = Ayudas.NuevaBase();
        db.Mensajes.Find(1)!.Fecha = DateTime.UtcNow.AddHours(-30);
        db.SaveChanges();
        string timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();

        await Recibir(db, Aviso("wamid.2", "56911111111", timestamp));

        Assert.Equal(2, db.Conversaciones.Count());
    }

    [Fact]
    public async Task Recibir_SinTimestamp_UsaLaHoraActual()
    {
        var db = Ayudas.NuevaBase();
        string cuerpo = Aviso("wamid.2", "56922222222", "").Replace("\"timestamp\":\"\",", "");

        await Recibir(db, cuerpo);

        var mensaje = db.Mensajes.Single(m => m.IdWhatsApp == "wamid.2");
        Assert.True(DateTime.UtcNow - mensaje.Fecha < TimeSpan.FromMinutes(1));
    }

    [Fact]
    public async Task Recibir_Imagen_GuardaElIdDeLaImagenYSuTexto()
    {
        var db = Ayudas.NuevaBase();
        string cuerpo = "{\"entry\":[{\"changes\":[{\"value\":{" +
                        "\"messages\":[{\"id\":\"wamid.foto\",\"from\":\"56911112222\",\"timestamp\":\"1761750000\"," +
                        "\"type\":\"image\",\"image\":{\"id\":\"123456\",\"mime_type\":\"image/jpeg\",\"caption\":\"Así van los enchufes\"}}]}}]}]}";

        await Recibir(db, cuerpo);

        var mensaje = db.Mensajes.Single(m => m.IdWhatsApp == "wamid.foto");
        Assert.Equal("123456", mensaje.IdImagen);
        Assert.Equal("[Imagen] Así van los enchufes", mensaje.Texto);
    }
}
