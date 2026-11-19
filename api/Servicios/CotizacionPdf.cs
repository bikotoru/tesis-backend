using System.Globalization;
using Backend.Modelos;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Backend.Servicios;

public static class CotizacionPdf
{
    const string ColorPrincipal = "#1F4E79";

    public static string NombreArchivo(Cotizacion cotizacion)
    {
        return $"Cotizacion-FerreLupe-{cotizacion.CotizacionId}.pdf";
    }

    public static string FechaEnPalabras(DateTime fechaUtc)
    {
        var zona = TimeZoneInfo.FindSystemTimeZoneById("America/Santiago");
        var fechaChile = TimeZoneInfo.ConvertTimeFromUtc(fechaUtc, zona);
        return fechaChile.ToString("d 'de' MMMM 'de' yyyy", new CultureInfo("es-CL"));
    }

    public static byte[] Generar(Cotizacion cotizacion)
    {
        QuestPDF.Settings.License = LicenseType.Community;

        var documento = Document.Create(contenedor =>
        {
            contenedor.Page(pagina =>
            {
                pagina.Size(PageSizes.Letter);
                pagina.Margin(40);
                pagina.DefaultTextStyle(estilo => estilo.FontSize(10));

                pagina.Header().Element(encabezado => Encabezado(encabezado, cotizacion));
                pagina.Content().Element(contenido => Contenido(contenido, cotizacion));
                pagina.Footer().AlignCenter().Text(texto =>
                {
                    texto.Span("Página ");
                    texto.CurrentPageNumber();
                    texto.Span(" de ");
                    texto.TotalPages();
                });
            });
        });

        return documento.GeneratePdf();
    }

    static void Encabezado(IContainer contenedor, Cotizacion cotizacion)
    {
        contenedor.BorderBottom(2).BorderColor(ColorPrincipal).PaddingBottom(10).Row(fila =>
        {
            fila.RelativeItem().Column(columna =>
            {
                columna.Item().Text("FerreLupe").FontSize(22).Bold().FontColor(ColorPrincipal);
                columna.Item().Text("Ferretería");
                columna.Item().Text("Los Lagos, Región de Los Ríos");
            });

            fila.ConstantItem(180).AlignRight().Column(columna =>
            {
                columna.Item().AlignRight().Text("COTIZACIÓN").FontSize(16).Bold().FontColor(ColorPrincipal);
                columna.Item().AlignRight().Text($"N.º {cotizacion.CotizacionId}");
                columna.Item().AlignRight().Text(FechaEnPalabras(cotizacion.Fecha));
            });
        });
    }

    static void Contenido(IContainer contenedor, Cotizacion cotizacion)
    {
        string nombre = "Sin nombre";
        string telefono = "";
        if (cotizacion.Cliente != null)
        {
            telefono = "+" + cotizacion.Cliente.Telefono;
            if (!string.IsNullOrEmpty(cotizacion.Cliente.Nombre))
            {
                nombre = cotizacion.Cliente.Nombre;
            }
        }

        contenedor.PaddingVertical(15).Column(columna =>
        {
            columna.Spacing(12);

            columna.Item().Column(cliente =>
            {
                cliente.Item().Text("Cliente").Bold().FontColor(ColorPrincipal);
                cliente.Item().Text("Nombre: " + nombre);
                cliente.Item().Text("WhatsApp: " + telefono);
            });

            columna.Item().Table(tabla =>
            {
                tabla.ColumnsDefinition(columnas =>
                {
                    columnas.RelativeColumn(5);
                    columnas.RelativeColumn(2);
                    columnas.RelativeColumn(2);
                    columnas.RelativeColumn(2);
                });

                tabla.Header(cabecera =>
                {
                    cabecera.Cell().Element(CeldaTitulo).Text("Producto");
                    cabecera.Cell().Element(CeldaTitulo).AlignRight().Text("Cantidad");
                    cabecera.Cell().Element(CeldaTitulo).AlignRight().Text("Precio unitario");
                    cabecera.Cell().Element(CeldaTitulo).AlignRight().Text("Subtotal");
                });

                foreach (var detalle in cotizacion.Detalles)
                {
                    long subtotal = (long)detalle.Cantidad * detalle.PrecioUnitario;
                    tabla.Cell().Element(Celda).Text(detalle.Producto!.Nombre);
                    tabla.Cell().Element(Celda).AlignRight().Text(detalle.Cantidad.ToString());
                    tabla.Cell().Element(Celda).AlignRight().Text(Cotizador.Pesos(detalle.PrecioUnitario));
                    tabla.Cell().Element(Celda).AlignRight().Text(Cotizador.Pesos(subtotal));
                }
            });

            columna.Item().AlignRight().Text("Total: " + Cotizador.Pesos(cotizacion.Total)).FontSize(14).Bold().FontColor(ColorPrincipal);

            columna.Item().PaddingTop(10).Column(notas =>
            {
                notas.Item().Text("Condiciones").Bold().FontColor(ColorPrincipal);
                notas.Item().Text("• Valores en pesos chilenos, con IVA incluido.");
                notas.Item().Text("• Precios referenciales, sujetos a stock al momento de la compra.");
                notas.Item().Text("• Para confirmar la compra, responde por WhatsApp indicando el número de esta cotización.");
            });
        });
    }

    static IContainer CeldaTitulo(IContainer contenedor)
    {
        return contenedor.Background(ColorPrincipal).Padding(5).DefaultTextStyle(estilo => estilo.FontColor(Colors.White).Bold());
    }

    static IContainer Celda(IContainer contenedor)
    {
        return contenedor.BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(5);
    }
}
