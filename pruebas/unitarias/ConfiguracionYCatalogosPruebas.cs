using Backend.Controllers;

namespace Pruebas;

public class ConfiguracionYCatalogosPruebas
{
    static DatosConfiguracion ConfiguracionValida()
    {
        var datos = new DatosConfiguracion();
        datos.MensajeBienvenida = "¡Hola! Soy el asistente de FerreLupe.";
        datos.Horario = "Lunes a viernes de 9:00 a 19:00";
        datos.RendimientoPintura = 12;
        datos.MargenMedidas = 15;
        return datos;
    }

    [Fact]
    public async Task Obtener_DevuelveElHorario()
    {
        var db = Ayudas.NuevaBase();

        var resultado = await new ConfiguracionController(db).Obtener();

        Assert.Equal("Lunes a sábado", Ayudas.Datos(resultado).GetProperty("Horario").GetString());
    }

    [Fact]
    public async Task Guardar_ConDatosValidos_CambiaElMargen()
    {
        var db = Ayudas.NuevaBase();

        await new ConfiguracionController(db).Guardar(ConfiguracionValida());

        Assert.Equal(15, db.Configuracion.Single().MargenMedidas);
    }

    [Fact]
    public async Task Guardar_ConMargenFueraDeRango_DevuelveError()
    {
        var db = Ayudas.NuevaBase();
        var datos = ConfiguracionValida();
        datos.MargenMedidas = 60;

        var resultado = await new ConfiguracionController(db).Guardar(datos);

        Assert.Equal("El margen debe estar entre 0 % y 50 %.", Ayudas.Mensaje(resultado));
    }

    [Fact]
    public async Task Guardar_SinMensajeDeBienvenida_PideElMensaje()
    {
        var db = Ayudas.NuevaBase();
        var datos = ConfiguracionValida();
        datos.MensajeBienvenida = "";

        var resultado = await new ConfiguracionController(db).Guardar(datos);

        Assert.Equal("Escribe el mensaje de bienvenida.", Ayudas.Mensaje(resultado));
    }

    [Fact]
    public async Task Categorias_DevuelveLaLista()
    {
        var db = Ayudas.NuevaBase();

        var resultado = await new CatalogosController(db).Categorias();

        Assert.Equal(1, Ayudas.Datos(resultado).GetArrayLength());
    }

    [Fact]
    public async Task Clientes_DevuelveLaLista()
    {
        var db = Ayudas.NuevaBase();

        var resultado = await new CatalogosController(db).Clientes();

        Assert.Equal(1, Ayudas.Datos(resultado).GetArrayLength());
    }
}
