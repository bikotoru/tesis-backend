using System.Net.Http.Json;
using System.Text.Json;
using Backend.Datos;
using Backend.Modelos;
using Microsoft.EntityFrameworkCore;

namespace Backend.Servicios;

public class ProductoErp
{
    public string Codigo { get; set; } = "";
    public string Nombre { get; set; } = "";
    public string Unidad { get; set; } = "";
    public int Precio { get; set; }
    public int Stock { get; set; }
}

public class ResultadoSincronizacion
{
    public int Actualizados { get; set; }
    public int Nuevos { get; set; }
}

public class Erp(AppDbContext db, HttpClient http, IConfiguration configuracion)
{
    static readonly JsonSerializerOptions opcionesJson = new JsonSerializerOptions(JsonSerializerDefaults.Web);

    public async Task<ResultadoSincronizacion> Sincronizar()
    {
        var productosErp = await LeerDelErp();

        var sinCategoria = await db.Categorias.FirstOrDefaultAsync(c => c.Nombre == "Sin categoría");
        if (sinCategoria == null)
        {
            sinCategoria = new Categoria { Nombre = "Sin categoría" };
            db.Categorias.Add(sinCategoria);
        }

        var existentes = await db.Productos.ToDictionaryAsync(p => p.Codigo, StringComparer.OrdinalIgnoreCase);
        var resultado = new ResultadoSincronizacion();
        foreach (var p in productosErp)
        {
            if (string.IsNullOrWhiteSpace(p.Codigo))
            {
                continue;
            }

            if (existentes.ContainsKey(p.Codigo))
            {
                var producto = existentes[p.Codigo];
                producto.Nombre = p.Nombre;
                producto.Unidad = p.Unidad;
                producto.Precio = p.Precio;
                producto.Stock = p.Stock;
                resultado.Actualizados++;
            }
            else
            {
                var nuevo = new Producto { Codigo = p.Codigo, Nombre = p.Nombre, Unidad = p.Unidad, Precio = p.Precio, Stock = p.Stock, Categoria = sinCategoria };
                db.Productos.Add(nuevo);
                existentes[p.Codigo] = nuevo;
                resultado.Nuevos++;
            }
        }

        var config = await db.Configuracion.FirstAsync();
        config.UltimaSincronizacion = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return resultado;
    }

    async Task<List<ProductoErp>> LeerDelErp()
    {
        if (configuracion.GetValue<bool>("Erp:Simulado"))
        {
            string ruta = Path.Combine(AppContext.BaseDirectory, "Datos", "erp-ejemplo.json");
            string contenido = await File.ReadAllTextAsync(ruta);
            var deEjemplo = JsonSerializer.Deserialize<List<ProductoErp>>(contenido, opcionesJson);
            return deEjemplo ?? [];
        }

        string? url = configuracion["Erp:Url"];
        if (string.IsNullOrEmpty(url))
        {
            throw new InvalidOperationException("Falta configurar la dirección del ERP (Erp__Url).");
        }
        var delErp = await http.GetFromJsonAsync<List<ProductoErp>>(url, opcionesJson);
        return delErp ?? [];
    }
}
