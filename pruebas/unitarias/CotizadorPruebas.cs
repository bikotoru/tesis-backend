using Backend.Modelos;
using Backend.Servicios;

namespace Pruebas;

public class CotizadorPruebas
{
    [Theory]
    [InlineData(890, "$890")]
    [InlineData(14990, "$14.990")]
    [InlineData(205510, "$205.510")]
    [InlineData(1500000, "$1.500.000")]
    public void Pesos_UsaPuntoDeMiles(int valor, string esperado)
    {
        string texto = Cotizador.Pesos(valor);

        Assert.Equal(esperado, texto);
    }

    [Fact]
    public async Task ArmarDetalles_TomaElPrecioDeLaBaseDeDatos()
    {
        var db = Ayudas.NuevaBase();
        var lineas = new List<Linea> { new Linea { ProductoId = 1, Cantidad = 2 } };

        var resultado = await Cotizador.ArmarDetalles(db, lineas);

        Assert.Null(resultado.Error);
        Assert.Single(resultado.Detalles);
        Assert.Equal(14990, resultado.Detalles[0].PrecioUnitario);
    }

    [Fact]
    public async Task ArmarDetalles_SumaLasCantidadesDelMismoProducto()
    {
        var db = Ayudas.NuevaBase();
        var lineas = new List<Linea> { new Linea { ProductoId = 1, Cantidad = 2 }, new Linea { ProductoId = 1, Cantidad = 3 } };

        var resultado = await Cotizador.ArmarDetalles(db, lineas);

        Assert.Equal(5, resultado.Detalles[0].Cantidad);
    }

    [Fact]
    public async Task ArmarDetalles_SinLineas_DevuelveError()
    {
        var db = Ayudas.NuevaBase();

        var resultado = await Cotizador.ArmarDetalles(db, new List<Linea>());

        Assert.Equal("La cotización debe tener al menos un producto.", resultado.Error);
    }

    [Fact]
    public async Task ArmarDetalles_LineaNula_DevuelveError()
    {
        var db = Ayudas.NuevaBase();
        var lineas = new List<Linea> { null! };

        var resultado = await Cotizador.ArmarDetalles(db, lineas);

        Assert.Equal("La cotización tiene una línea vacía.", resultado.Error);
    }

    [Fact]
    public async Task ArmarDetalles_CantidadCero_DevuelveError()
    {
        var db = Ayudas.NuevaBase();
        var lineas = new List<Linea> { new Linea { ProductoId = 1, Cantidad = 0 } };

        var resultado = await Cotizador.ArmarDetalles(db, lineas);

        Assert.Equal("La cantidad de cada producto debe ser mayor que cero.", resultado.Error);
    }

    [Fact]
    public async Task ArmarDetalles_CantidadMayorA10000_DevuelveError()
    {
        var db = Ayudas.NuevaBase();
        var lineas = new List<Linea> { new Linea { ProductoId = 1, Cantidad = 6000 }, new Linea { ProductoId = 1, Cantidad = 5000 } };

        var resultado = await Cotizador.ArmarDetalles(db, lineas);

        Assert.Equal("La cantidad de cada producto no puede superar 10.000.", resultado.Error);
    }

    [Fact]
    public async Task ArmarDetalles_ProductoInactivo_DevuelveError()
    {
        var db = Ayudas.NuevaBase();
        var lineas = new List<Linea> { new Linea { ProductoId = 3, Cantidad = 1 } };

        var resultado = await Cotizador.ArmarDetalles(db, lineas);

        Assert.Equal("El producto 3 no existe o no está activo.", resultado.Error);
    }

    [Fact]
    public async Task ArmarDetalles_SinStockSuficiente_DevuelveError()
    {
        var db = Ayudas.NuevaBase();
        var lineas = new List<Linea> { new Linea { ProductoId = 2, Cantidad = 6 } };

        var resultado = await Cotizador.ArmarDetalles(db, lineas);

        Assert.Equal("No hay stock suficiente de «Rodillo»: quedan 5.", resultado.Error);
    }

    [Fact]
    public void Texto_IncluyeElDetalleYElTotal()
    {
        var producto = new Producto { Nombre = "Rodillo" };
        var cotizacion = new Cotizacion { CotizacionId = 7 };
        cotizacion.Detalles.Add(new DetalleCotizacion { Producto = producto, Cantidad = 2, PrecioUnitario = 3990 });

        string texto = Cotizador.Texto(cotizacion);

        Assert.Contains("Cotización N.º 7", texto);
        Assert.Contains("• Rodillo: 2 × $3.990 = $7.980", texto);
        Assert.Contains("Total: $7.980 (IVA incluido)", texto);
    }

    static Cotizacion CotizacionDeEjemplo()
    {
        var producto = new Producto { ProductoId = 1, Nombre = "Rodillo", Unidad = "unidad", Precio = 3990 };
        var cotizacion = new Cotizacion { CotizacionId = 7, Fecha = new DateTime(2026, 10, 29, 15, 0, 0, DateTimeKind.Utc) };
        cotizacion.Cliente = new Cliente { Telefono = "56971252508", Nombre = "Víctor" };
        cotizacion.Detalles.Add(new DetalleCotizacion { ProductoId = 1, Producto = producto, Cantidad = 2, PrecioUnitario = 3990 });
        return cotizacion;
    }

    [Fact]
    public void Pdf_GeneraUnArchivoPdf()
    {
        byte[] pdf = CotizacionPdf.Generar(CotizacionDeEjemplo());

        string inicio = System.Text.Encoding.ASCII.GetString(pdf, 0, 4);
        Assert.Equal("%PDF", inicio);
    }

    [Fact]
    public void Pdf_NombreArchivoLlevaElNumero()
    {
        string nombre = CotizacionPdf.NombreArchivo(CotizacionDeEjemplo());

        Assert.Equal("Cotizacion-FerreLupe-7.pdf", nombre);
    }

    [Fact]
    public void Pdf_FechaEnPalabrasConHoraDeChile()
    {
        string fecha = CotizacionPdf.FechaEnPalabras(new DateTime(2026, 10, 30, 1, 0, 0, DateTimeKind.Utc));

        Assert.Equal("29 de octubre de 2026", fecha);
    }
}
