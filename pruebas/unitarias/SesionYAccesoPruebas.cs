using Backend.Controllers;
using Backend.Modelos;
using Backend.Servicios;
using Microsoft.AspNetCore.Identity;

namespace Pruebas;

public class SesionYAccesoPruebas
{
    static AuthController NuevoAuth(Backend.Datos.AppDbContext db)
    {
        return new AuthController(db, new PasswordHasher<Usuario>(), new Sesion(Ayudas.Configuracion()));
    }

    [Fact]
    public void CrearToken_DevuelveUnTokenConTresPartes()
    {
        var sesion = new Sesion(Ayudas.Configuracion());

        string token = sesion.CrearToken(1, "Admin", "Administrador");

        Assert.Equal(3, token.Split('.').Length);
    }

    [Fact]
    public async Task SigueValida_UsuarioActivoConSuRol_EsValida()
    {
        var db = Ayudas.NuevaBase();

        bool valida = await Sesion.SigueValida(db, Ayudas.Usuario(1, "Administrador"));

        Assert.True(valida);
    }

    [Fact]
    public async Task SigueValida_UsuarioDesactivado_NoEsValida()
    {
        var db = Ayudas.NuevaBase();
        db.Usuarios.Find(2)!.Activo = false;
        db.SaveChanges();

        bool valida = await Sesion.SigueValida(db, Ayudas.Usuario(2, "Vendedor"));

        Assert.False(valida);
    }

    [Fact]
    public async Task SigueValida_RolDistintoAlDelToken_NoEsValida()
    {
        var db = Ayudas.NuevaBase();

        bool valida = await Sesion.SigueValida(db, Ayudas.Usuario(2, "Administrador"));

        Assert.False(valida);
    }

    [Fact]
    public async Task SigueValida_UsuarioInexistente_NoEsValida()
    {
        var db = Ayudas.NuevaBase();

        bool valida = await Sesion.SigueValida(db, Ayudas.Usuario(99, "Vendedor"));

        Assert.False(valida);
    }

    [Fact]
    public async Task Ingresar_ConDatosCorrectos_DevuelveElToken()
    {
        var db = Ayudas.NuevaBase();
        var datos = new DatosIngreso { Correo = " admin@ferrelupe.cl ", Clave = Ayudas.ClaveDePrueba };

        var resultado = await NuevoAuth(db).Ingresar(datos);

        Assert.Equal(200, Ayudas.Estado(resultado));
        Assert.Equal("Administrador", Ayudas.Datos(resultado).GetProperty("rol").GetString());
    }

    [Fact]
    public async Task Ingresar_ConClaveIncorrecta_Devuelve401()
    {
        var db = Ayudas.NuevaBase();
        var datos = new DatosIngreso { Correo = "admin@ferrelupe.cl", Clave = "otra-clave" };

        var resultado = await NuevoAuth(db).Ingresar(datos);

        Assert.Equal(401, Ayudas.Estado(resultado));
        Assert.Equal("El correo o la contraseña no son correctos.", Ayudas.Mensaje(resultado));
    }

    [Fact]
    public async Task Ingresar_ConCorreoInexistente_DevuelveElMismoMensaje()
    {
        var db = Ayudas.NuevaBase();
        var datos = new DatosIngreso { Correo = "nadie@ferrelupe.cl", Clave = Ayudas.ClaveDePrueba };

        var resultado = await NuevoAuth(db).Ingresar(datos);

        Assert.Equal(401, Ayudas.Estado(resultado));
        Assert.Equal("El correo o la contraseña no son correctos.", Ayudas.Mensaje(resultado));
    }

    [Fact]
    public async Task Ingresar_ConCuentaDesactivada_Devuelve401()
    {
        var db = Ayudas.NuevaBase();
        db.Usuarios.Find(2)!.Activo = false;
        db.SaveChanges();
        var datos = new DatosIngreso { Correo = "venta@ferrelupe.cl", Clave = Ayudas.ClaveDePrueba };

        var resultado = await NuevoAuth(db).Ingresar(datos);

        Assert.Equal("Tu cuenta está desactivada. Habla con el administrador.", Ayudas.Mensaje(resultado));
    }

    [Fact]
    public async Task Ingresar_SinCorreo_PideElCorreo()
    {
        var db = Ayudas.NuevaBase();
        var datos = new DatosIngreso { Correo = "", Clave = "x" };

        var resultado = await NuevoAuth(db).Ingresar(datos);

        Assert.Equal("Escribe tu correo.", Ayudas.Mensaje(resultado));
    }

    [Fact]
    public async Task Ingresar_SinClave_PideLaClave()
    {
        var db = Ayudas.NuevaBase();
        var datos = new DatosIngreso { Correo = "admin@ferrelupe.cl", Clave = "" };

        var resultado = await NuevoAuth(db).Ingresar(datos);

        Assert.Equal("Escribe tu contraseña.", Ayudas.Mensaje(resultado));
    }
}
