using System.Globalization;
using Backend.Datos;
using Backend.Modelos;
using Microsoft.EntityFrameworkCore;

namespace Backend.Servicios;

public class Linea
{
    public int ProductoId { get; set; }
    public int Cantidad { get; set; }
}

public class DetallesArmados
{
    public List<DetalleCotizacion> Detalles { get; set; } = [];
    public string? Error { get; set; }
}

public static class Cotizador
{
    public const int CantidadMaxima = 10000;

    public static async Task<DetallesArmados> ArmarDetalles(AppDbContext db, List<Linea>? lineas)
    {
        var resultado = new DetallesArmados();
        if (lineas == null || lineas.Count == 0)
        {
            resultado.Error = "La cotización debe tener al menos un producto.";
            return resultado;
        }

        var cantidades = new Dictionary<int, int>();
        foreach (var linea in lineas)
        {
            if (linea == null)
            {
                resultado.Error = "La cotización tiene una línea vacía.";
                return resultado;
            }
            if (linea.Cantidad < 1)
            {
                resultado.Error = "La cantidad de cada producto debe ser mayor que cero.";
                return resultado;
            }

            if (cantidades.ContainsKey(linea.ProductoId))
            {
                cantidades[linea.ProductoId] = cantidades[linea.ProductoId] + linea.Cantidad;
            }
            else
            {
                cantidades[linea.ProductoId] = linea.Cantidad;
            }

            if (cantidades[linea.ProductoId] > CantidadMaxima)
            {
                resultado.Error = "La cantidad de cada producto no puede superar 10.000.";
                return resultado;
            }
        }

        var ids = cantidades.Keys.ToList();
        var productos = await db.Productos.Where(p => ids.Contains(p.ProductoId)).ToDictionaryAsync(p => p.ProductoId);

        foreach (var productoId in ids)
        {
            int cantidad = cantidades[productoId];
            if (!productos.ContainsKey(productoId) || !productos[productoId].Activo)
            {
                resultado.Error = $"El producto {productoId} no existe o no está activo.";
                resultado.Detalles.Clear();
                return resultado;
            }

            var producto = productos[productoId];
            if (cantidad > producto.Stock)
            {
                resultado.Error = $"No hay stock suficiente de «{producto.Nombre}»: quedan {producto.Stock}.";
                resultado.Detalles.Clear();
                return resultado;
            }

            var detalle = new DetalleCotizacion
            {
                ProductoId = producto.ProductoId,
                Producto = producto,
                Cantidad = cantidad,
                PrecioUnitario = producto.Precio,
            };
            resultado.Detalles.Add(detalle);
        }
        return resultado;
    }

    public static string Texto(Cotizacion cotizacion)
    {
        var texto = $"Cotización N.º {cotizacion.CotizacionId}\n";
        foreach (var d in cotizacion.Detalles)
        {
            long subtotal = (long)d.Cantidad * d.PrecioUnitario;
            texto += $"• {d.Producto!.Nombre}: {d.Cantidad} × {Pesos(d.PrecioUnitario)} = {Pesos(subtotal)}\n";
        }
        texto += $"Total: {Pesos(cotizacion.Total)} (IVA incluido)\nPrecios referenciales, sujetos a stock.";
        return texto;
    }

    public static string Pesos(long valor)
    {
        string conComas = valor.ToString("N0", CultureInfo.InvariantCulture);
        return "$" + conComas.Replace(',', '.');
    }
}
