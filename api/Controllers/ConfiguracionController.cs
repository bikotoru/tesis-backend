using Backend.Datos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Backend.Controllers;

public class DatosConfiguracion
{
    public string MensajeBienvenida { get; set; } = "";
    public string Horario { get; set; } = "";
    public decimal RendimientoPintura { get; set; }
    public int MargenMedidas { get; set; }
}

[ApiController]
[Route("api/configuracion")]
[Authorize(Roles = "Administrador")]
public class ConfiguracionController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Obtener()
    {
        var c = await db.Configuracion.FirstAsync();
        return Ok(new { c.MensajeBienvenida, c.Horario, c.RendimientoPintura, c.MargenMedidas, c.UltimaSincronizacion });
    }

    [HttpPut]
    public async Task<IActionResult> Guardar(DatosConfiguracion datos)
    {
        string? error = Validar(datos);
        if (error != null)
        {
            return BadRequest(new { mensaje = error });
        }

        var c = await db.Configuracion.FirstAsync();
        c.MensajeBienvenida = datos.MensajeBienvenida.Trim();
        c.Horario = datos.Horario.Trim();
        c.RendimientoPintura = datos.RendimientoPintura;
        c.MargenMedidas = datos.MargenMedidas;
        await db.SaveChangesAsync();
        return Ok(new { mensaje = "La configuración se guardó. El asistente la usa desde el próximo mensaje." });
    }

    static string? Validar(DatosConfiguracion datos)
    {
        if (string.IsNullOrWhiteSpace(datos.MensajeBienvenida))
        {
            return "Escribe el mensaje de bienvenida.";
        }
        if (datos.MensajeBienvenida.Trim().Length > 500)
        {
            return "El mensaje de bienvenida admite hasta 500 caracteres.";
        }
        if (string.IsNullOrWhiteSpace(datos.Horario))
        {
            return "Escribe el horario.";
        }
        if (datos.Horario.Trim().Length > 100)
        {
            return "El horario admite hasta 100 caracteres.";
        }
        if (datos.RendimientoPintura < 1 || datos.RendimientoPintura > 30)
        {
            return "El rendimiento debe estar entre 1 y 30 m² por litro.";
        }
        if (datos.MargenMedidas < 0 || datos.MargenMedidas > 50)
        {
            return "El margen debe estar entre 0 % y 50 %.";
        }
        return null;
    }
}
