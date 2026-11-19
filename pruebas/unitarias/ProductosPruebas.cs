using Backend.Controllers;
using Backend.Servicios;
using Microsoft.Extensions.Logging.Abstractions;

namespace Pruebas;

public class ProductosPruebas
{
    static DatosProducto ProductoValido()
    {
        var datos = new DatosProducto();
        datos.Codigo = "PIN-010";
        datos.Nombre = "Brocha 2 pulgadas";
        datos.CategoriaId = 1;
        datos.Unidad = "unidad";
        datos.Precio = 2490;
        datos.Stock = 20;
        datos.Descripcion = "  Brocha de cerda.  ";
        datos.Activo = true;
        return datos;
    }

    [Fact]
    public async Task Listar_ConBusqueda_FiltraPorNombre()
    {
        var db = Ayudas.NuevaBase();

        var resultado = await new ProductosController(db).Listar("Rodillo");

        Assert.Equal(1, Ayudas.Datos(resultado).GetArrayLength());
    }

    [Fact]
    public async Task Obtener_ProductoInexistente_Devuelve404()
    {
        var db = Ayudas.NuevaBase();

        var resultado = await new ProductosController(db).Obtener(99);

        Assert.Equal(404, Ayudas.Estado(resultado));
    }

    [Fact]
    public async Task Obtener_ProductoExistente_DevuelveSusDatos()
    {
        var db = Ayudas.NuevaBase();

        var resultado = await new ProductosController(db).Obtener(1);

        Assert.Equal("PIN-001", Ayudas.Datos(resultado).GetProperty("Codigo").GetString());
    }

    [Fact]
    public async Task Crear_ConDatosValidos_GuardaElProducto()
    {
        var db = Ayudas.NuevaBase();

        var resultado = await new ProductosController(db).Crear(ProductoValido());

        Assert.Equal(200, Ayudas.Estado(resultado));
        Assert.Equal("Brocha de cerda.", db.Productos.Single(p => p.Codigo == "PIN-010").Descripcion);
    }

    [Fact]
    public async Task Crear_ConCodigoRepetido_DevuelveError()
    {
        var db = Ayudas.NuevaBase();
        var datos = ProductoValido();
        datos.Codigo = "PIN-001";

        var resultado = await new ProductosController(db).Crear(datos);

        Assert.Equal("Ya existe otro producto con ese código.", Ayudas.Mensaje(resultado));
    }

    [Fact]
    public async Task Crear_SinCodigo_PideElCodigo()
    {
        var db = Ayudas.NuevaBase();
        var datos = ProductoValido();
        datos.Codigo = " ";

        var resultado = await new ProductosController(db).Crear(datos);

        Assert.Equal("Escribe el código.", Ayudas.Mensaje(resultado));
    }

    [Fact]
    public async Task Crear_ConPrecioNegativo_DevuelveError()
    {
        var db = Ayudas.NuevaBase();
        var datos = ProductoValido();
        datos.Precio = -1;

        var resultado = await new ProductosController(db).Crear(datos);

        Assert.Equal("El precio no puede ser negativo.", Ayudas.Mensaje(resultado));
    }

    [Fact]
    public async Task Crear_ConCategoriaInexistente_DevuelveError()
    {
        var db = Ayudas.NuevaBase();
        var datos = ProductoValido();
        datos.CategoriaId = 99;

        var resultado = await new ProductosController(db).Crear(datos);

        Assert.Equal("La categoría no existe.", Ayudas.Mensaje(resultado));
    }

    [Fact]
    public async Task Editar_CambiaElPrecio()
    {
        var db = Ayudas.NuevaBase();
        var datos = ProductoValido();
        datos.Codigo = "PIN-002";
        datos.Precio = 4500;

        await new ProductosController(db).Editar(2, datos);

        Assert.Equal(4500, db.Productos.Find(2)!.Precio);
    }

    [Fact]
    public async Task Editar_ProductoInexistente_Devuelve404()
    {
        var db = Ayudas.NuevaBase();

        var resultado = await new ProductosController(db).Editar(99, ProductoValido());

        Assert.Equal(404, Ayudas.Estado(resultado));
    }

    [Fact]
    public async Task SugerirDescripcion_EnModoSimulado_DevuelveUnTexto()
    {
        var db = Ayudas.NuevaBase();
        var datos = new DatosSugerencia { Nombre = "Rodillo" };

        var resultado = await new ProductosController(db).SugerirDescripcion(datos, Ayudas.NuevoAsistente(db));

        Assert.Contains("Rodillo", Ayudas.Datos(resultado).GetProperty("descripcion").GetString());
    }

    [Fact]
    public async Task SugerirDescripcion_SinNombre_PideElNombre()
    {
        var db = Ayudas.NuevaBase();

        var resultado = await new ProductosController(db).SugerirDescripcion(new DatosSugerencia(), Ayudas.NuevoAsistente(db));

        Assert.Equal("Escribe el nombre del producto.", Ayudas.Mensaje(resultado));
    }

    [Fact]
    public async Task Sincronizar_EnModoSimulado_AgregaLosProductosDelErp()
    {
        var db = Ayudas.NuevaBase();
        var erp = new Erp(db, new HttpClient(), Ayudas.Configuracion());

        var resultado = await new ProductosController(db).Sincronizar(erp, NullLogger<ProductosController>.Instance);

        Assert.Equal(200, Ayudas.Estado(resultado));
        Assert.NotNull(db.Configuracion.Single().UltimaSincronizacion);
    }

    [Fact]
    public async Task Sincronizar_ComparaLosCodigosSinDistinguirMayusculas()
    {
        var db = Ayudas.NuevaBase();
        db.Productos.Add(new Backend.Modelos.Producto { ProductoId = 10, CategoriaId = 1, Codigo = "ele-001", Nombre = "Cable", Unidad = "metro" });
        db.SaveChanges();
        var erp = new Erp(db, new HttpClient(), Ayudas.Configuracion());

        await erp.Sincronizar();

        Assert.Equal(890, db.Productos.Find(10)!.Precio);
        Assert.Equal(0, db.Productos.Count(p => p.Codigo == "ELE-001"));
    }

    [Fact]
    public async Task Sincronizar_SinDireccionDelErp_Devuelve502()
    {
        var db = Ayudas.NuevaBase();
        var configuracion = new Microsoft.Extensions.Configuration.ConfigurationBuilder().Build();
        var erp = new Erp(db, new HttpClient(), configuracion);

        var resultado = await new ProductosController(db).Sincronizar(erp, NullLogger<ProductosController>.Instance);

        Assert.Equal(502, Ayudas.Estado(resultado));
    }
}
