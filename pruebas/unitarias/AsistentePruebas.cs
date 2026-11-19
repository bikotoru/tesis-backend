using System.Text.Json;
using Anthropic.Models.Messages;

namespace Pruebas;

public class AsistentePruebas
{
    static Dictionary<string, JsonElement> Datos(string json)
    {
        return JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json)!;
    }

    static async Task<Backend.Servicios.ResultadoHerramienta> Ejecutar(string herramienta, string json)
    {
        var db = Ayudas.NuevaBase();
        var conversacion = db.Conversaciones.Find(1)!;
        var config = db.Configuracion.Single();
        var resultado = await Ayudas.NuevasHerramientas(db).Ejecutar(herramienta, Datos(json), conversacion, config);
        return resultado;
    }

    [Fact]
    public async Task Responder_EnModoSimulado_EnviaUnaRespuesta()
    {
        var db = Ayudas.NuevaBase();

        await Ayudas.NuevoAsistente(db).Responder(1, 1);

        Assert.Contains(db.Mensajes, m => m.Emisor == "Asistente");
    }

    [Fact]
    public async Task Responder_SiLlegoOtroMensaje_NoResponde()
    {
        var db = Ayudas.NuevaBase();
        Ayudas.AgregarMensajeDelCliente(db, 2, 1, DateTime.UtcNow);

        await Ayudas.NuevoAsistente(db).Responder(1, 1);

        Assert.DoesNotContain(db.Mensajes, m => m.Emisor == "Asistente");
    }

    [Fact]
    public async Task Responder_SiUnVendedorTomoLaConversacion_NoResponde()
    {
        var db = Ayudas.NuevaBase();
        db.Conversaciones.Find(1)!.Estado = "Vendedor";
        db.SaveChanges();

        await Ayudas.NuevoAsistente(db).Responder(1, 1);

        Assert.DoesNotContain(db.Mensajes, m => m.Emisor == "Asistente");
    }

    [Fact]
    public async Task Responder_SiNoSePuedeEnviar_DerivaAlVendedor()
    {
        var db = Ayudas.NuevaBase();
        db.Mensajes.Find(1)!.Fecha = DateTime.UtcNow.AddHours(-25);
        db.SaveChanges();

        await Ayudas.NuevoAsistente(db).Responder(1, 1);

        Assert.Equal("Vendedor", db.Conversaciones.Find(1)!.Estado);
    }

    [Fact]
    public async Task CalcularPintura_DevuelveLosLitros()
    {
        var resultado = await Ejecutar("calcular_pintura", "{\"largo\":5,\"ancho\":2,\"alto\":2.4}");

        Assert.False(resultado.EsError);
        Assert.Equal(8, JsonDocument.Parse(resultado.Texto).RootElement.GetProperty("litros").GetInt32());
    }

    [Fact]
    public async Task CalcularPintura_ConManosFueraDeRango_DevuelveError()
    {
        var resultado = await Ejecutar("calcular_pintura", "{\"largo\":5,\"ancho\":2,\"alto\":2.4,\"manos\":6}");

        Assert.True(resultado.EsError);
        Assert.Equal("La cantidad de manos debe estar entre 1 y 4.", resultado.Texto);
    }

    [Fact]
    public async Task CalcularPintura_ConMedidasInvalidas_DevuelveError()
    {
        var resultado = await Ejecutar("calcular_pintura", "{\"largo\":50,\"ancho\":2,\"alto\":2.4}");

        Assert.Equal("El largo y el ancho deben estar entre 0,5 y 30 metros.", resultado.Texto);
    }

    [Fact]
    public async Task CalcularElectricidad_DevuelveElCable()
    {
        var resultado = await Ejecutar("calcular_electricidad", "{\"largo\":5,\"ancho\":2,\"alto\":2.4,\"enchufes\":10}");

        Assert.False(resultado.EsError);
        Assert.Equal(44, JsonDocument.Parse(resultado.Texto).RootElement.GetProperty("cable_2_5_mm2_por_color_m").GetInt32());
    }

    [Fact]
    public async Task CalcularElectricidad_ConDemasiadosEnchufes_DevuelveError()
    {
        var resultado = await Ejecutar("calcular_electricidad", "{\"largo\":5,\"ancho\":2,\"alto\":2.4,\"enchufes\":60}");

        Assert.Equal("La cantidad de enchufes debe estar entre 1 y 50.", resultado.Texto);
    }

    [Fact]
    public async Task CalcularElectricidad_ConDistanciaInvalida_DevuelveError()
    {
        var resultado = await Ejecutar("calcular_electricidad", "{\"largo\":5,\"ancho\":2,\"alto\":2.4,\"enchufes\":4,\"distancia_tablero\":200}");

        Assert.Equal("La distancia al tablero debe estar entre 0 y 100 metros.", resultado.Texto);
    }

    [Fact]
    public async Task BuscarProductos_EncuentraPorNombre()
    {
        var resultado = await Ejecutar("buscar_productos", "{\"texto\":\"Rodillo\"}");

        Assert.Contains("Rodillo", resultado.Texto);
    }

    [Fact]
    public async Task BuscarProductos_SinResultados_SugiereOtraPalabra()
    {
        var resultado = await Ejecutar("buscar_productos", "{\"texto\":\"Martillo\"}");

        Assert.Equal("No hay productos que coincidan. Prueba con otra palabra más general.", resultado.Texto);
    }

    [Fact]
    public async Task EnviarCotizacion_GuardaYEnvia()
    {
        var resultado = await Ejecutar("enviar_cotizacion", "{\"productos\":[{\"producto_id\":1,\"cantidad\":2}]}");

        Assert.False(resultado.EsError);
        Assert.Contains("$29.980", resultado.Texto);
    }

    [Fact]
    public async Task EnviarCotizacion_SinStock_DevuelveError()
    {
        var resultado = await Ejecutar("enviar_cotizacion", "{\"productos\":[{\"producto_id\":2,\"cantidad\":50}]}");

        Assert.True(resultado.EsError);
    }

    [Fact]
    public async Task DerivarAVendedor_CambiaElEstado()
    {
        var db = Ayudas.NuevaBase();
        var conversacion = db.Conversaciones.Find(1)!;

        await Ayudas.NuevasHerramientas(db).Ejecutar("derivar_a_vendedor", Datos("{}"), conversacion, db.Configuracion.Single());

        Assert.Equal("Vendedor", db.Conversaciones.Find(1)!.Estado);
    }

    [Fact]
    public void Herramientas_TieneLasSieteQueEjecutaElAsistente()
    {
        var nombres = new List<string>();

        foreach (var herramienta in Backend.Servicios.HerramientasNormales.Lista)
        {
            if (herramienta.TryPickTool(out Tool? definicion))
            {
                nombres.Add(definicion.Name);
            }
        }

        Assert.Equal(7, nombres.Count);
        Assert.Contains("elegir_envases", nombres);
        Assert.Contains("calcular_cables_en_tubo", nombres);
        Assert.Contains("buscar_productos", nombres);
        Assert.Contains("calcular_pintura", nombres);
        Assert.Contains("calcular_electricidad", nombres);
        Assert.Contains("enviar_cotizacion", nombres);
        Assert.Contains("derivar_a_vendedor", nombres);
    }

    [Fact]
    public async Task HerramientaDesconocida_DevuelveError()
    {
        var resultado = await Ejecutar("borrar_todo", "{}");

        Assert.Equal("Herramienta desconocida.", resultado.Texto);
    }

    [Fact]
    public async Task HerramientaConDatosFaltantes_DevuelveError()
    {
        var resultado = await Ejecutar("calcular_pintura", "{\"largo\":5}");

        Assert.Equal("Faltan datos o tienen un formato incorrecto.", resultado.Texto);
    }
}
