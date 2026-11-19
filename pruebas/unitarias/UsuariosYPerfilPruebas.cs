using Backend.Controllers;
using Backend.Datos;
using Backend.Modelos;
using Microsoft.AspNetCore.Identity;

namespace Pruebas;

public class UsuariosYPerfilPruebas
{
    static UsuariosController NuevoUsuarios(AppDbContext db)
    {
        var controlador = new UsuariosController(db, new PasswordHasher<Usuario>());
        return Ayudas.ConUsuario(controlador, 1, "Administrador");
    }

    static PerfilController NuevoPerfil(AppDbContext db)
    {
        var controlador = new PerfilController(db, new PasswordHasher<Usuario>());
        return Ayudas.ConUsuario(controlador, 2, "Vendedor");
    }

    static DatosUsuario UsuarioValido()
    {
        var datos = new DatosUsuario();
        datos.Nombre = "Pedro";
        datos.Correo = "pedro@ferrelupe.cl";
        datos.Rol = "Vendedor";
        datos.Activo = true;
        datos.Clave = "clave-segura";
        return datos;
    }

    [Fact]
    public async Task Listar_DevuelveTodosLosUsuarios()
    {
        var db = Ayudas.NuevaBase();

        var resultado = await NuevoUsuarios(db).Listar();

        Assert.Equal(2, Ayudas.Datos(resultado).GetArrayLength());
    }

    [Fact]
    public async Task Obtener_UsuarioInexistente_Devuelve404()
    {
        var db = Ayudas.NuevaBase();

        var resultado = await NuevoUsuarios(db).Obtener(99);

        Assert.Equal(404, Ayudas.Estado(resultado));
    }

    [Fact]
    public async Task Obtener_UsuarioExistente_DevuelveSuRol()
    {
        var db = Ayudas.NuevaBase();

        var resultado = await NuevoUsuarios(db).Obtener(2);

        Assert.Equal("Vendedor", Ayudas.Datos(resultado).GetProperty("rol").GetString());
    }

    [Fact]
    public async Task Crear_ConDatosValidos_GuardaLaCuenta()
    {
        var db = Ayudas.NuevaBase();

        var resultado = await NuevoUsuarios(db).Crear(UsuarioValido());

        Assert.Equal(200, Ayudas.Estado(resultado));
        Assert.Equal(2, db.Usuarios.Single(u => u.Correo == "pedro@ferrelupe.cl").RolId);
    }

    [Fact]
    public async Task Crear_SinClave_PideUnaClaveInicial()
    {
        var db = Ayudas.NuevaBase();
        var datos = UsuarioValido();
        datos.Clave = null;

        var resultado = await NuevoUsuarios(db).Crear(datos);

        Assert.Equal("Escribe una contraseña inicial.", Ayudas.Mensaje(resultado));
    }

    [Fact]
    public async Task Crear_ConCorreoInvalido_DevuelveError()
    {
        var db = Ayudas.NuevaBase();
        var datos = UsuarioValido();
        datos.Correo = "pedro.ferrelupe.cl";

        var resultado = await NuevoUsuarios(db).Crear(datos);

        Assert.Equal("El correo no es válido.", Ayudas.Mensaje(resultado));
    }

    [Fact]
    public async Task Crear_ConPerfilDesconocido_DevuelveError()
    {
        var db = Ayudas.NuevaBase();
        var datos = UsuarioValido();
        datos.Rol = "Gerente";

        var resultado = await NuevoUsuarios(db).Crear(datos);

        Assert.Equal("El perfil debe ser Administrador o Vendedor.", Ayudas.Mensaje(resultado));
    }

    [Fact]
    public async Task Crear_ConCorreoRepetido_DevuelveError()
    {
        var db = Ayudas.NuevaBase();
        var datos = UsuarioValido();
        datos.Correo = "venta@ferrelupe.cl";

        var resultado = await NuevoUsuarios(db).Crear(datos);

        Assert.Equal("Ese correo ya lo usa otra cuenta.", Ayudas.Mensaje(resultado));
    }

    [Fact]
    public async Task Editar_DesactivaOtraCuenta()
    {
        var db = Ayudas.NuevaBase();
        var datos = UsuarioValido();
        datos.Correo = "venta@ferrelupe.cl";
        datos.Activo = false;
        datos.Clave = null;

        await NuevoUsuarios(db).Editar(2, datos);

        Assert.False(db.Usuarios.Find(2)!.Activo);
    }

    [Fact]
    public async Task Editar_NoPermiteDesactivarLaPropiaCuenta()
    {
        var db = Ayudas.NuevaBase();
        var datos = UsuarioValido();
        datos.Correo = "admin@ferrelupe.cl";
        datos.Rol = "Administrador";
        datos.Activo = false;
        datos.Clave = null;

        var resultado = await NuevoUsuarios(db).Editar(1, datos);

        Assert.Equal("No puedes desactivar tu propia cuenta ni quitarte el perfil de administrador.", Ayudas.Mensaje(resultado));
    }

    [Fact]
    public async Task PerfilObtener_DevuelveLosDatosDelUsuario()
    {
        var db = Ayudas.NuevaBase();

        var resultado = await NuevoPerfil(db).Obtener();

        Assert.Equal("venta@ferrelupe.cl", Ayudas.Datos(resultado).GetProperty("Correo").GetString());
    }

    [Fact]
    public async Task PerfilGuardar_CambiaElNombre()
    {
        var db = Ayudas.NuevaBase();
        var datos = new DatosPerfil { Nombre = " María ", Correo = "venta@ferrelupe.cl" };

        await NuevoPerfil(db).Guardar(datos);

        Assert.Equal("María", db.Usuarios.Find(2)!.Nombre);
    }

    [Fact]
    public async Task PerfilGuardar_ConCorreoDeOtraCuenta_DevuelveError()
    {
        var db = Ayudas.NuevaBase();
        var datos = new DatosPerfil { Nombre = "María", Correo = "admin@ferrelupe.cl" };

        var resultado = await NuevoPerfil(db).Guardar(datos);

        Assert.Equal("Ese correo ya lo usa otra cuenta.", Ayudas.Mensaje(resultado));
    }

    [Fact]
    public async Task PerfilGuardar_SinNombre_PideElNombre()
    {
        var db = Ayudas.NuevaBase();
        var datos = new DatosPerfil { Nombre = "", Correo = "venta@ferrelupe.cl" };

        var resultado = await NuevoPerfil(db).Guardar(datos);

        Assert.Equal("Escribe tu nombre.", Ayudas.Mensaje(resultado));
    }

    [Fact]
    public async Task CambiarClave_ConClaveActualIncorrecta_DevuelveError()
    {
        var db = Ayudas.NuevaBase();
        var datos = new CambioClave { Actual = "otra-clave", Nueva = "nueva-clave" };

        var resultado = await NuevoPerfil(db).CambiarClave(datos);

        Assert.Equal("La contraseña actual no es correcta.", Ayudas.Mensaje(resultado));
    }

    [Fact]
    public async Task CambiarClave_ConClaveNuevaCorta_DevuelveError()
    {
        var db = Ayudas.NuevaBase();
        var datos = new CambioClave { Actual = Ayudas.ClaveDePrueba, Nueva = "corta" };

        var resultado = await NuevoPerfil(db).CambiarClave(datos);

        Assert.Equal("La contraseña nueva debe tener al menos 8 caracteres.", Ayudas.Mensaje(resultado));
    }

    [Fact]
    public async Task CambiarClave_ConDatosCorrectos_CambiaLaClave()
    {
        var db = Ayudas.NuevaBase();
        string hashAnterior = db.Usuarios.Find(2)!.ClaveHash;
        var datos = new CambioClave { Actual = Ayudas.ClaveDePrueba, Nueva = "nueva-clave" };

        var resultado = await NuevoPerfil(db).CambiarClave(datos);

        Assert.Equal(200, Ayudas.Estado(resultado));
        Assert.NotEqual(hashAnterior, db.Usuarios.Find(2)!.ClaveHash);
    }
}
