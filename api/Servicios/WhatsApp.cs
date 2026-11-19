using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;

namespace Backend.Servicios;

public class WhatsApp(HttpClient http, IConfiguration configuracion, ILogger<WhatsApp> log)
{
    const string VersionGraph = "v26.0";

    bool Simulado()
    {
        return configuracion.GetValue<bool>("WhatsApp:Simulado");
    }

    public async Task<bool> Enviar(string telefono, string texto)
    {
        if (Simulado())
        {
            log.LogInformation("WhatsApp simulado a {Telefono}: {Texto}", telefono, texto);
            return true;
        }

        string url = $"https://graph.facebook.com/{VersionGraph}/{configuracion["WhatsApp:IdNumero"]}/messages";
        var solicitud = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = JsonContent.Create(new
            {
                messaging_product = "whatsapp",
                recipient_type = "individual",
                to = telefono,
                type = "text",
                text = new { body = texto },
            }),
        };
        solicitud.Headers.Authorization = new AuthenticationHeaderValue("Bearer", configuracion["WhatsApp:Token"]);

        var respuesta = await http.SendAsync(solicitud);
        if (!respuesta.IsSuccessStatusCode)
        {
            log.LogError("WhatsApp rechazó el mensaje: {Estado} {Detalle}", respuesta.StatusCode, await respuesta.Content.ReadAsStringAsync());
        }
        return respuesta.IsSuccessStatusCode;
    }

    public async Task<bool> EnviarDocumento(string telefono, byte[] archivo, string nombreArchivo, string texto)
    {
        if (Simulado())
        {
            log.LogInformation("WhatsApp simulado a {Telefono}: documento {Archivo}", telefono, nombreArchivo);
            return true;
        }

        string? idArchivo = await SubirArchivo(archivo, nombreArchivo, "application/pdf");
        if (idArchivo == null)
        {
            return false;
        }

        string url = $"https://graph.facebook.com/{VersionGraph}/{configuracion["WhatsApp:IdNumero"]}/messages";
        var solicitud = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = JsonContent.Create(new
            {
                messaging_product = "whatsapp",
                recipient_type = "individual",
                to = telefono,
                type = "document",
                document = new { id = idArchivo, caption = texto, filename = nombreArchivo },
            }),
        };
        solicitud.Headers.Authorization = new AuthenticationHeaderValue("Bearer", configuracion["WhatsApp:Token"]);

        var respuesta = await http.SendAsync(solicitud);
        if (!respuesta.IsSuccessStatusCode)
        {
            log.LogError("WhatsApp rechazó el documento: {Estado} {Detalle}", respuesta.StatusCode, await respuesta.Content.ReadAsStringAsync());
        }
        return respuesta.IsSuccessStatusCode;
    }

    public async Task<bool> EnviarImagen(string telefono, byte[] imagen, string tipo, string texto)
    {
        if (Simulado())
        {
            log.LogInformation("WhatsApp simulado a {Telefono}: imagen {Tipo}", telefono, tipo);
            return true;
        }

        string nombreArchivo = "imagen.jpg";
        if (tipo == "image/png")
        {
            nombreArchivo = "imagen.png";
        }
        string? idArchivo = await SubirArchivo(imagen, nombreArchivo, tipo);
        if (idArchivo == null)
        {
            return false;
        }

        string url = $"https://graph.facebook.com/{VersionGraph}/{configuracion["WhatsApp:IdNumero"]}/messages";
        var solicitud = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = JsonContent.Create(new
            {
                messaging_product = "whatsapp",
                recipient_type = "individual",
                to = telefono,
                type = "image",
                image = new { id = idArchivo, caption = texto },
            }),
        };
        solicitud.Headers.Authorization = new AuthenticationHeaderValue("Bearer", configuracion["WhatsApp:Token"]);

        var respuesta = await http.SendAsync(solicitud);
        if (!respuesta.IsSuccessStatusCode)
        {
            log.LogError("WhatsApp rechazó la imagen: {Estado} {Detalle}", respuesta.StatusCode, await respuesta.Content.ReadAsStringAsync());
        }
        return respuesta.IsSuccessStatusCode;
    }

    async Task<string?> SubirArchivo(byte[] archivo, string nombreArchivo, string tipo)
    {
        var contenidoArchivo = new ByteArrayContent(archivo);
        contenidoArchivo.Headers.ContentType = new MediaTypeHeaderValue(tipo);

        var formulario = new MultipartFormDataContent();
        formulario.Add(new StringContent("whatsapp"), "messaging_product");
        formulario.Add(new StringContent(tipo), "type");
        formulario.Add(contenidoArchivo, "file", nombreArchivo);

        string url = $"https://graph.facebook.com/{VersionGraph}/{configuracion["WhatsApp:IdNumero"]}/media";
        var solicitud = new HttpRequestMessage(HttpMethod.Post, url);
        solicitud.Content = formulario;
        solicitud.Headers.Authorization = new AuthenticationHeaderValue("Bearer", configuracion["WhatsApp:Token"]);

        var respuesta = await http.SendAsync(solicitud);
        if (!respuesta.IsSuccessStatusCode)
        {
            log.LogError("WhatsApp no aceptó el archivo: {Estado} {Detalle}", respuesta.StatusCode, await respuesta.Content.ReadAsStringAsync());
            return null;
        }

        var datos = await respuesta.Content.ReadFromJsonAsync<ArchivoSubido>();
        if (datos == null)
        {
            return null;
        }
        return datos.id;
    }

    public async Task<ImagenDescargada?> DescargarImagen(string idImagen)
    {
        if (Simulado())
        {
            return null;
        }

        string url = $"https://graph.facebook.com/{VersionGraph}/{idImagen}";
        var pedirUrl = new HttpRequestMessage(HttpMethod.Get, url);
        pedirUrl.Headers.Authorization = new AuthenticationHeaderValue("Bearer", configuracion["WhatsApp:Token"]);
        var respuesta = await http.SendAsync(pedirUrl);
        if (!respuesta.IsSuccessStatusCode)
        {
            log.LogWarning("No se encontró la imagen {Id}: {Estado}", idImagen, respuesta.StatusCode);
            return null;
        }

        var datos = await respuesta.Content.ReadFromJsonAsync<DatosDeImagen>();
        if (datos == null || datos.url == "")
        {
            return null;
        }

        var pedirArchivo = new HttpRequestMessage(HttpMethod.Get, datos.url);
        pedirArchivo.Headers.Authorization = new AuthenticationHeaderValue("Bearer", configuracion["WhatsApp:Token"]);
        var archivo = await http.SendAsync(pedirArchivo);
        if (!archivo.IsSuccessStatusCode)
        {
            log.LogWarning("No se pudo descargar la imagen {Id}: {Estado}", idImagen, archivo.StatusCode);
            return null;
        }

        var imagen = new ImagenDescargada();
        imagen.Datos = await archivo.Content.ReadAsByteArrayAsync();
        imagen.Tipo = datos.mime_type;
        return imagen;
    }

    public static bool FirmaValida(byte[] cuerpo, string? encabezado, string? secreto)
    {
        if (string.IsNullOrEmpty(encabezado) || string.IsNullOrEmpty(secreto) || !encabezado.StartsWith("sha256="))
        {
            return false;
        }

        byte[] esperada = HMACSHA256.HashData(Encoding.UTF8.GetBytes(secreto), cuerpo);
        byte[] recibida;
        try
        {
            recibida = Convert.FromHexString(encabezado.Substring("sha256=".Length));
        }
        catch (FormatException)
        {
            return false;
        }
        return CryptographicOperations.FixedTimeEquals(esperada, recibida);
    }
}

public class ArchivoSubido
{
    public string id { get; set; } = "";
}

public class DatosDeImagen
{
    public string url { get; set; } = "";
    public string mime_type { get; set; } = "";
}

public class ImagenDescargada
{
    public byte[] Datos { get; set; } = [];
    public string Tipo { get; set; } = "";
}
