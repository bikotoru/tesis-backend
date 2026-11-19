using System.Text.Json;
using Backend.Datos;
using Backend.Modelos;
using Backend.Servicios;

namespace Pruebas;

public class EnvasesYTubosPruebas
{
    static List<Envase> GalonYTineta(int stockTineta)
    {
        var envases = new List<Envase>();
        envases.Add(new Envase { ProductoId = 1, Nombre = "Galón", Litros = 3.785, Precio = 15990, Stock = 12 });
        envases.Add(new Envase { ProductoId = 2, Nombre = "Tineta", Litros = 15.14, Precio = 54990, Stock = stockTineta });
        return envases;
    }

    static int Cantidad(CombinacionEnvases combinacion, int productoId)
    {
        foreach (var elegido in combinacion.Elegidos)
        {
            if (elegido.Envase.ProductoId == productoId)
            {
                return elegido.Cantidad;
            }
        }
        return 0;
    }

    [Fact]
    public void Envases_PocosLitros_ConvienenGalones()
    {
        var combinacion = Calculos.EnvasesMasBaratos(8, GalonYTineta(6))!;

        Assert.Equal(3, Cantidad(combinacion, 1));
        Assert.Equal(0, Cantidad(combinacion, 2));
        Assert.Equal(47970, combinacion.Precio);
    }

    [Fact]
    public void Envases_ConLitroSuelto_DosGalonesYUnLitro()
    {
        var envases = GalonYTineta(6);
        envases.Add(new Envase { ProductoId = 3, Nombre = "Litro", Litros = 1, Precio = 5490, Stock = 20 });

        var combinacion = Calculos.EnvasesMasBaratos(8, envases)!;

        Assert.Equal(2, Cantidad(combinacion, 1));
        Assert.Equal(1, Cantidad(combinacion, 3));
        Assert.Equal(37470, combinacion.Precio);
    }

    [Fact]
    public void Envases_CatorceLitros_ConvieneUnaTineta()
    {
        var combinacion = Calculos.EnvasesMasBaratos(14, GalonYTineta(6))!;

        Assert.Equal(0, Cantidad(combinacion, 1));
        Assert.Equal(1, Cantidad(combinacion, 2));
        Assert.Equal(54990, combinacion.Precio);
    }

    [Fact]
    public void Envases_TreintaLitros_ConvienenDosTinetas()
    {
        var combinacion = Calculos.EnvasesMasBaratos(30, GalonYTineta(6))!;

        Assert.Equal(2, Cantidad(combinacion, 2));
        Assert.Equal(109980, combinacion.Precio);
        Assert.Equal(30.28, combinacion.Litros);
    }

    [Fact]
    public void Envases_RespetaElStock()
    {
        var combinacion = Calculos.EnvasesMasBaratos(30, GalonYTineta(1))!;

        Assert.Equal(1, Cantidad(combinacion, 2));
        Assert.Equal(4, Cantidad(combinacion, 1));
        Assert.Equal(118950, combinacion.Precio);
    }

    [Fact]
    public void Envases_SinStockSuficiente_DevuelveNull()
    {
        var envases = new List<Envase>();
        envases.Add(new Envase { ProductoId = 1, Nombre = "Galón", Litros = 3.785, Precio = 15990, Stock = 1 });

        var combinacion = Calculos.EnvasesMasBaratos(8, envases);

        Assert.Null(combinacion);
    }

    [Fact]
    public void Tubo_NueveCablesDe2_5EnTuboDe20_NoCaben()
    {
        var r = Calculos.CablesEnTubo(2.5, 9, 20);

        Assert.False(r.Caben);
        Assert.Equal(0, r.DiametroMinimo);
        Assert.Equal(5, r.MaximoEnElTubo);
        Assert.Equal(2, r.TubosNecesarios);
        Assert.Equal(20, r.DiametroDeLosTubos);
    }

    [Fact]
    public void Tubo_TresCablesDe2_5SinDiametro_NecesitaTuboDe20()
    {
        var r = Calculos.CablesEnTubo(2.5, 3, 0);

        Assert.True(r.Caben);
        Assert.Equal(20, r.DiametroMinimo);
        Assert.Equal(1, r.TubosNecesarios);
    }

    [Fact]
    public void Tubo_CuatroCablesDe2_5EnTuboDe16_NoCaben()
    {
        var r = Calculos.CablesEnTubo(2.5, 4, 16);

        Assert.False(r.Caben);
        Assert.Equal(20, r.DiametroMinimo);
        Assert.Equal(2, r.MaximoEnElTubo);
        Assert.Equal(2, r.TubosNecesarios);
        Assert.Equal(16, r.DiametroDeLosTubos);
    }

    [Fact]
    public void Tubo_DosCablesDe1_5EnTuboDeDiametroMenorQueLaTabla_RecomiendaUnoDe16()
    {
        var r = Calculos.CablesEnTubo(1.5, 2, 12);

        Assert.False(r.Caben);
        Assert.Equal(1, r.TubosNecesarios);
        Assert.Equal(16, r.DiametroDeLosTubos);
    }

    [Fact]
    public void Tubo_SeccionFueraDeLaTabla_NoSeConoce()
    {
        Assert.True(Calculos.SeccionConocida(2.5));
        Assert.False(Calculos.SeccionConocida(6));
    }

    [Fact]
    public void Electricidad_IndicaTuboDe20ParaTresCablesDe2_5()
    {
        var r = Calculos.Electricidad(3, 3, 2.4, 2, 5, 10);

        Assert.Equal(20, r.DiametroTubo);
    }

    static Dictionary<string, JsonElement> Datos(string json)
    {
        return JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json)!;
    }

    static async Task<ResultadoHerramienta> Ejecutar(AppDbContext db, string herramienta, string json)
    {
        var conversacion = db.Conversaciones.Find(1)!;
        var config = db.Configuracion.Single();
        var resultado = await Ayudas.NuevasHerramientas(db).Ejecutar(herramienta, Datos(json), conversacion, config);
        return resultado;
    }

    static void AgregarMarfil(AppDbContext db)
    {
        db.Productos.Add(new Producto { ProductoId = 20, CategoriaId = 1, Codigo = "PIN-007", Nombre = "Látex marfil (galón)", Unidad = "galón", Precio = 15990, Stock = 12, Litros = 3.785m });
        db.Productos.Add(new Producto { ProductoId = 21, CategoriaId = 1, Codigo = "PIN-008", Nombre = "Látex marfil (tineta)", Unidad = "tineta", Precio = 54990, Stock = 6, Litros = 15.14m });
        db.SaveChanges();
    }

    [Fact]
    public async Task ElegirEnvases_DevuelveLaTineta()
    {
        var db = Ayudas.NuevaBase();
        AgregarMarfil(db);

        var resultado = await Ejecutar(db, "elegir_envases", """{"litros": 14, "pintura": "marfil"}""");

        Assert.False(resultado.EsError);
        Assert.Contains("\"producto_id\":21", resultado.Texto);
    }

    [Fact]
    public async Task ElegirEnvases_NoMezclaColores()
    {
        var db = Ayudas.NuevaBase();
        AgregarMarfil(db);
        db.Productos.Add(new Producto { ProductoId = 22, CategoriaId = 1, Codigo = "PIN-009", Nombre = "Látex blanco (galón)", Unidad = "galón", Precio = 15490, Stock = 1, Litros = 3.785m });
        db.SaveChanges();

        var resultado = await Ejecutar(db, "elegir_envases", """{"litros": 18, "pintura": "Látex"}""");

        using var json = JsonDocument.Parse(resultado.Texto);
        var opciones = json.RootElement.GetProperty("opciones");
        Assert.Equal(1, opciones.GetArrayLength());
        Assert.Equal("Látex marfil", opciones[0].GetProperty("pintura").GetString());
    }

    [Fact]
    public void NombreSinEnvase_QuitaLoQueVaEntreParentesis()
    {
        Assert.Equal("Látex color marfil", HerramientasNormales.NombreSinEnvase("Látex color marfil (tineta 4 galones)"));
        Assert.Equal("Rodillo", HerramientasNormales.NombreSinEnvase("Rodillo"));
    }

    [Fact]
    public async Task ElegirEnvases_PinturaSinLitros_DevuelveError()
    {
        var db = Ayudas.NuevaBase();

        var resultado = await Ejecutar(db, "elegir_envases", """{"litros": 8, "pintura": "blanco"}""");

        Assert.True(resultado.EsError);
    }

    [Fact]
    public async Task ElegirEnvases_LitrosFueraDeRango_DevuelveError()
    {
        var db = Ayudas.NuevaBase();

        var resultado = await Ejecutar(db, "elegir_envases", """{"litros": 0, "pintura": "marfil"}""");

        Assert.True(resultado.EsError);
    }

    [Fact]
    public async Task ElegirEnvases_SinStockSuficiente_DevuelveError()
    {
        var db = Ayudas.NuevaBase();
        AgregarMarfil(db);

        var resultado = await Ejecutar(db, "elegir_envases", """{"litros": 400, "pintura": "marfil"}""");

        Assert.True(resultado.EsError);
    }

    [Fact]
    public async Task CablesEnTubo_NueveCables_DiceQueNoCaben()
    {
        var db = Ayudas.NuevaBase();

        var resultado = await Ejecutar(db, "calcular_cables_en_tubo", """{"seccion_mm2": 2.5, "cables": 9, "diametro_tubo_mm": 20}""");

        Assert.False(resultado.EsError);
        Assert.Contains("\"caben\":false", resultado.Texto);
        Assert.Contains("\"tubos_necesarios\":2", resultado.Texto);
    }

    [Fact]
    public async Task CablesEnTubo_SeccionDesconocida_DevuelveError()
    {
        var db = Ayudas.NuevaBase();

        var resultado = await Ejecutar(db, "calcular_cables_en_tubo", """{"seccion_mm2": 6, "cables": 3}""");

        Assert.True(resultado.EsError);
    }

    [Fact]
    public async Task CablesEnTubo_DemasiadosCables_DevuelveError()
    {
        var db = Ayudas.NuevaBase();

        var resultado = await Ejecutar(db, "calcular_cables_en_tubo", """{"seccion_mm2": 2.5, "cables": 80}""");

        Assert.True(resultado.EsError);
    }
}
