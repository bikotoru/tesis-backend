using Backend.Datos;
using Backend.Modelos;
using Backend.Servicios;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Backend.Controllers;

public class DatosUsuario
{
    public string Nombre { get; set; } = "";
    public string Correo { get; set; } = "";
    public string Rol { get; set; } = "";
    public bool Activo { get; set; }
    public string? Clave { get; set; }
}

[ApiController]
[Route("api/usuarios")]
[Authorize(Roles = "Administrador")]
public class UsuariosController(AppDbContext db, IPasswordHasher<Usuario> hasher) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Listar()
    {
        var ordenados = db.Usuarios.OrderBy(u => u.Nombre);
        var lista = await ordenados.Select(u => new { id = u.UsuarioId, u.Nombre, u.Correo, rol = u.Rol!.Nombre, u.Activo }).ToListAsync();
        return Ok(lista);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> Obtener(int id)
    {
        var consulta = db.Usuarios.Where(u => u.UsuarioId == id);
        var usuario = await consulta.Select(u => new { id = u.UsuarioId, u.Nombre, u.Correo, rol = u.Rol!.Nombre, u.Activo }).FirstOrDefaultAsync();
        if (usuario == null)
        {
            return NotFound(new { mensaje = "No se encontró el usuario." });
        }
        return Ok(usuario);
    }

    [HttpPost]
    public async Task<IActionResult> Crear(DatosUsuario datos)
    {
        string? error = Validar(datos);
        if (error != null)
        {
            return BadRequest(new { mensaje = error });
        }
        if (string.IsNullOrEmpty(datos.Clave))
        {
            return BadRequest(new { mensaje = "Escribe una contraseña inicial." });
        }

        string correo = datos.Correo.Trim();
        bool correoOcupado = await db.Usuarios.AnyAsync(u => u.Correo == correo);
        if (correoOcupado)
        {
            return BadRequest(new { mensaje = "Ese correo ya lo usa otra cuenta." });
        }

        var usuario = new Usuario();
        usuario.Nombre = datos.Nombre.Trim();
        usuario.Correo = correo;
        usuario.Activo = datos.Activo;
        usuario.RolId = await IdDelRol(datos.Rol);
        usuario.ClaveHash = hasher.HashPassword(usuario, datos.Clave);
        db.Usuarios.Add(usuario);
        await db.SaveChangesAsync();
        return Ok(new { id = usuario.UsuarioId });
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Editar(int id, DatosUsuario datos)
    {
        string? error = Validar(datos);
        if (error != null)
        {
            return BadRequest(new { mensaje = error });
        }

        var usuario = await db.Usuarios.FirstOrDefaultAsync(u => u.UsuarioId == id);
        if (usuario == null)
        {
            return NotFound(new { mensaje = "No se encontró el usuario." });
        }

        string correo = datos.Correo.Trim();
        bool correoOcupado = await db.Usuarios.AnyAsync(u => u.Correo == correo && u.UsuarioId != id);
        if (correoOcupado)
        {
            return BadRequest(new { mensaje = "Ese correo ya lo usa otra cuenta." });
        }

        bool esSuPropiaCuenta = id == Sesion.UsuarioId(User);
        if (esSuPropiaCuenta && (!datos.Activo || datos.Rol != "Administrador"))
        {
            return BadRequest(new { mensaje = "No puedes desactivar tu propia cuenta ni quitarte el perfil de administrador." });
        }

        usuario.Nombre = datos.Nombre.Trim();
        usuario.Correo = correo;
        usuario.Activo = datos.Activo;
        usuario.RolId = await IdDelRol(datos.Rol);
        await db.SaveChangesAsync();
        return Ok(new { id });
    }

    public static bool CorreoValido(string correo)
    {
        int arroba = correo.IndexOf('@');
        if (arroba <= 0)
        {
            return false;
        }
        if (arroba != correo.LastIndexOf('@'))
        {
            return false;
        }
        return arroba < correo.Length - 1;
    }

    static string? Validar(DatosUsuario datos)
    {
        if (string.IsNullOrWhiteSpace(datos.Nombre))
        {
            return "Escribe el nombre.";
        }
        if (datos.Nombre.Trim().Length > 100)
        {
            return "El nombre admite hasta 100 caracteres.";
        }
        if (string.IsNullOrWhiteSpace(datos.Correo))
        {
            return "Escribe el correo.";
        }
        string correo = datos.Correo.Trim();
        if (!CorreoValido(correo))
        {
            return "El correo no es válido.";
        }
        if (correo.Length > 150)
        {
            return "El correo admite hasta 150 caracteres.";
        }
        if (string.IsNullOrWhiteSpace(datos.Rol))
        {
            return "Elige el perfil.";
        }
        if (datos.Rol != "Administrador" && datos.Rol != "Vendedor")
        {
            return "El perfil debe ser Administrador o Vendedor.";
        }
        if (datos.Clave != null && datos.Clave.Length < 8)
        {
            return "La contraseña debe tener al menos 8 caracteres.";
        }
        return null;
    }

    async Task<int> IdDelRol(string nombre)
    {
        var rol = await db.Roles.FirstAsync(r => r.Nombre == nombre);
        return rol.RolId;
    }
}
