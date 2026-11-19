using System.Text.Encodings.Web;
using System.Text.Json;
using Anthropic.Models.Messages;
using Backend.Datos;
using Backend.Modelos;
using Microsoft.EntityFrameworkCore;

namespace Backend.Servicios;

public class ResultadoHerramienta
{
    public string Texto { get; set; } = "";
    public bool EsError { get; set; }
}

public class HerramientasNormales(AppDbContext db, Mensajeria mensajeria, ILogger log)
{
    static readonly JsonSerializerOptions json = new JsonSerializerOptions { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    public bool SeDerivo { get; set; }

    public static readonly List<ToolUnion> Lista =
    [
        new Tool
        {
            Name = "buscar_productos",
            Description = "Busca productos activos del catálogo de FerreLupe por nombre y descripción. Devuelve id, nombre, unidad, precio (pesos chilenos, IVA incluido), stock y descripción. Máximo 20 resultados.",
            InputSchema = new()
            {
                Properties = new Dictionary<string, JsonElement>
                {
                    ["texto"] = Campo("string", "Una o dos palabras clave, por ejemplo «látex» o «cable 2,5»."),
                },
                Required = ["texto"],
            },
        },
        new Tool
        {
            Name = "calcular_pintura",
            Description = "Calcula la pintura para los muros de una habitación (y el cielo, si se pide), con el margen y el rendimiento configurados. Devuelve el área y los litros, redondeados hacia arriba.",
            InputSchema = new()
            {
                Properties = new Dictionary<string, JsonElement>
                {
                    ["largo"] = Campo("number", "Largo de la habitación en metros."),
                    ["ancho"] = Campo("number", "Ancho de la habitación en metros."),
                    ["alto"] = Campo("number", "Alto de los muros en metros."),
                    ["manos"] = Campo("integer", "Manos de pintura (por defecto, 2)."),
                    ["cielo"] = Campo("boolean", "true si también se pinta el cielo."),
                },
                Required = ["largo", "ancho", "alto"],
            },
        },
        new Tool
        {
            Name = "calcular_electricidad",
            Description = "Calcula los materiales para instalar enchufes en una habitación: cable de 2,5 mm² por color, tiras de tubería, enchufes, cajas y protecciones, con el margen configurado.",
            InputSchema = new()
            {
                Properties = new Dictionary<string, JsonElement>
                {
                    ["largo"] = Campo("number", "Largo de la habitación en metros."),
                    ["ancho"] = Campo("number", "Ancho de la habitación en metros."),
                    ["alto"] = Campo("number", "Alto de los muros en metros."),
                    ["enchufes"] = Campo("integer", "Cantidad de enchufes."),
                    ["distancia_tablero"] = Campo("number", "Distancia aproximada al tablero eléctrico en metros (por defecto, 5)."),
                },
                Required = ["largo", "ancho", "alto", "enchufes"],
            },
        },
        new Tool
        {
            Name = "elegir_envases",
            Description = "Elige la combinación más barata de envases (litros, galones, tinetas u otros) para cubrir los litros calculados, usando solo productos activos con stock y con sus litros registrados. Devuelve una opción por cada pintura y color; nunca mezcla colores.",
            InputSchema = new()
            {
                Properties = new Dictionary<string, JsonElement>
                {
                    ["litros"] = Campo("number", "Litros que se necesitan (el resultado de calcular_pintura)."),
                    ["pintura"] = Campo("string", "Una o dos palabras de la pintura, con el color si lo dijo el cliente, por ejemplo «látex marfil»."),
                },
                Required = ["litros", "pintura"],
            },
        },
        new Tool
        {
            Name = "calcular_cables_en_tubo",
            Description = "Indica si una cantidad de cables cabe en un tubo, según la Tabla N.º 4.17 del Pliego RIC N.º 04 de la SEC (cables unipolares de 1,5, 2,5 o 4 mm², hasta 5 conductores por tubo). Devuelve el diámetro mínimo y, si no caben, cuántos tubos se necesitan.",
            InputSchema = new()
            {
                Properties = new Dictionary<string, JsonElement>
                {
                    ["seccion_mm2"] = Campo("number", "Sección del cable en mm²: 1,5, 2,5 o 4."),
                    ["cables"] = Campo("integer", "Cantidad total de cables dentro del tubo."),
                    ["diametro_tubo_mm"] = Campo("integer", "Diámetro del tubo que tiene o quiere usar el cliente, en mm (0 si no lo sabe)."),
                },
                Required = ["seccion_mm2", "cables"],
            },
        },
        new Tool
        {
            Name = "enviar_cotizacion",
            Description = "Guarda la cotización y se la envía al cliente por WhatsApp como documento PDF con el detalle y el total. Usa solo ids que devolvió buscar_productos. El sistema toma los precios del catálogo y revisa el stock.",
            InputSchema = new()
            {
                Properties = new Dictionary<string, JsonElement>
                {
                    ["productos"] = JsonSerializer.SerializeToElement(new
                    {
                        type = "array",
                        description = "Productos de la cotización.",
                        items = new
                        {
                            type = "object",
                            properties = new
                            {
                                producto_id = new { type = "integer", description = "Id del producto." },
                                cantidad = new { type = "integer", description = "Cantidad en la unidad de venta del producto." },
                            },
                            required = new[] { "producto_id", "cantidad" },
                        },
                    }),
                },
                Required = ["productos"],
            },
        },
        new Tool
        {
            Name = "derivar_a_vendedor",
            Description = "Pasa la conversación a un vendedor cuando el cliente pide algo que el asistente no puede resolver (descuentos, despachos, reclamos, formas de pago) o quiere hablar con una persona.",
            InputSchema = new() { Properties = new Dictionary<string, JsonElement>() },
        },
    ];

    public async Task<ResultadoHerramienta> Ejecutar(string nombre, IReadOnlyDictionary<string, JsonElement> datos, Conversacion conversacion, Configuracion config)
    {
        try
        {
            if (nombre == "buscar_productos")
            {
                return await BuscarProductos(Texto(datos, "texto"));
            }
            if (nombre == "calcular_pintura")
            {
                return CalcularPintura(datos, config);
            }
            if (nombre == "calcular_electricidad")
            {
                return CalcularElectricidad(datos, config);
            }
            if (nombre == "elegir_envases")
            {
                return await ElegirEnvases(datos);
            }
            if (nombre == "calcular_cables_en_tubo")
            {
                return CalcularCablesEnTubo(datos);
            }
            if (nombre == "enviar_cotizacion")
            {
                return await EnviarCotizacion(datos, conversacion);
            }
            if (nombre == "derivar_a_vendedor")
            {
                await Derivar(conversacion.ConversacionId);
                return Correcto("Listo: la conversación quedó para un vendedor. Despídete avisando que en un momento lo atiende un vendedor.");
            }
            return ConError("Herramienta desconocida.");
        }
        catch (Exception e) when (e is KeyNotFoundException || e is InvalidOperationException || e is FormatException)
        {
            return ConError("Faltan datos o tienen un formato incorrecto.");
        }
    }

    async Task<ResultadoHerramienta> BuscarProductos(string busqueda)
    {
        var consulta = db.Productos.Where(p => p.Activo);
        foreach (var palabra in busqueda.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            consulta = consulta.Where(p => p.Nombre.Contains(palabra) || (p.Descripcion != null && p.Descripcion.Contains(palabra)));
        }

        var primeros = consulta.OrderBy(p => p.Nombre).Take(20);
        var productos = await primeros.Select(p => new
        {
            id = p.ProductoId,
            nombre = p.Nombre,
            unidad = p.Unidad,
            precio = p.Precio,
            stock = p.Stock,
            litros = p.Litros,
            descripcion = p.Descripcion,
        }).ToListAsync();

        if (productos.Count == 0)
        {
            return Correcto("No hay productos que coincidan. Prueba con otra palabra más general.");
        }
        return Correcto(JsonSerializer.Serialize(productos, json));
    }

    static ResultadoHerramienta CalcularPintura(IReadOnlyDictionary<string, JsonElement> datos, Configuracion config)
    {
        double largo = Numero(datos, "largo");
        double ancho = Numero(datos, "ancho");
        double alto = Numero(datos, "alto");
        int manos = 2;
        if (datos.ContainsKey("manos"))
        {
            manos = (int)Numero(datos, "manos");
        }
        bool cielo = datos.ContainsKey("cielo") && datos["cielo"].ValueKind == JsonValueKind.True;

        string? error = Calculos.ValidarHabitacion(largo, ancho, alto);
        if (error != null)
        {
            return ConError(error);
        }
        if (manos < 1 || manos > 4)
        {
            return ConError("La cantidad de manos debe estar entre 1 y 4.");
        }

        var r = Calculos.Pintura(largo, ancho, alto, manos, cielo, (double)config.RendimientoPintura, config.MargenMedidas);
        string resultado = JsonSerializer.Serialize(new
        {
            area_m2 = r.Area,
            area_con_margen_m2 = r.AreaConMargen,
            margen_porcentaje = config.MargenMedidas,
            manos,
            litros = r.Litros,
            accesorios_sugeridos = new[] { "rodillo", "bandeja", "brocha", "cinta de enmascarar" },
        }, json);
        return Correcto(resultado);
    }

    static ResultadoHerramienta CalcularElectricidad(IReadOnlyDictionary<string, JsonElement> datos, Configuracion config)
    {
        double largo = Numero(datos, "largo");
        double ancho = Numero(datos, "ancho");
        double alto = Numero(datos, "alto");
        int enchufes = (int)Numero(datos, "enchufes");
        double distancia = 5;
        if (datos.ContainsKey("distancia_tablero"))
        {
            distancia = Numero(datos, "distancia_tablero");
        }

        string? error = Calculos.ValidarHabitacion(largo, ancho, alto);
        if (error != null)
        {
            return ConError(error);
        }
        if (enchufes < 1 || enchufes > 50)
        {
            return ConError("La cantidad de enchufes debe estar entre 1 y 50.");
        }
        if (distancia < 0 || distancia > 100)
        {
            return ConError("La distancia al tablero debe estar entre 0 y 100 metros.");
        }

        var r = Calculos.Electricidad(largo, ancho, alto, enchufes, distancia, config.MargenMedidas);
        string resultado = JsonSerializer.Serialize(new
        {
            recorrido_m = r.Recorrido,
            recorrido_con_margen_m = r.RecorridoConMargen,
            margen_porcentaje = config.MargenMedidas,
            cable_2_5_mm2_por_color_m = r.CablePorColor,
            colores_de_cable = new[] { "fase (rojo)", "neutro (blanco)", "tierra (verde/amarillo)" },
            tiras_tuberia_3_m = r.TirasTuberia,
            nota_tuberia = "Cantidad máxima si todo el recorrido va entubado; el instalador autorizado define la cantidad final.",
            enchufes_dobles = r.Enchufes,
            cajas_de_enchufe = r.CajasEnchufe,
            cajas_de_derivacion = r.CajasDerivacion,
            interruptor_automatico_16_a = r.Automaticos,
            protector_diferencial_30_ma = r.Diferenciales,
            diametro_tuberia_mm = r.DiametroTubo,
        }, json);
        return Correcto(resultado);
    }

    async Task<ResultadoHerramienta> EnviarCotizacion(IReadOnlyDictionary<string, JsonElement> datos, Conversacion conversacion)
    {
        var lineas = new List<Linea>();
        foreach (var p in datos["productos"].EnumerateArray())
        {
            var linea = new Linea();
            linea.ProductoId = p.GetProperty("producto_id").GetInt32();
            linea.Cantidad = p.GetProperty("cantidad").GetInt32();
            lineas.Add(linea);
        }

        var armados = await Cotizador.ArmarDetalles(db, lineas);
        if (armados.Error != null)
        {
            return ConError(armados.Error);
        }

        var cotizacion = new Cotizacion
        {
            ClienteId = conversacion.ClienteId,
            ConversacionId = conversacion.ConversacionId,
            Detalles = armados.Detalles,
        };
        db.Cotizaciones.Add(cotizacion);
        await db.SaveChangesAsync();

        string? aviso = await mensajeria.EnviarCotizacion(conversacion.ConversacionId, cotizacion, "Asistente");
        if (aviso != null)
        {
            log.LogWarning("No se pudo enviar la cotización {Id}: {Aviso}", cotizacion.CotizacionId, aviso);
            await Derivar(conversacion.ConversacionId);
            return ConError(aviso);
        }

        string total = Cotizador.Pesos(cotizacion.Total);
        return Correcto($"Cotización N.º {cotizacion.CotizacionId} enviada al cliente, con un total de {total}. " +
                        "No repitas el detalle ni los montos: cierra con una frase breve.");
    }

    async Task<ResultadoHerramienta> ElegirEnvases(IReadOnlyDictionary<string, JsonElement> datos)
    {
        double litros = Numero(datos, "litros");
        string pintura = Texto(datos, "pintura");
        if (litros <= 0 || litros > 500)
        {
            return ConError("Los litros deben ser mayores que 0 y hasta 500.");
        }

        var consulta = db.Productos.Where(p => p.Activo && p.Litros != null && p.Stock > 0);
        foreach (var palabra in pintura.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            consulta = consulta.Where(p => p.Nombre.Contains(palabra));
        }
        var productos = await consulta.ToListAsync();
        if (productos.Count == 0)
        {
            return ConError("No hay envases con stock de esa pintura. Busca con buscar_productos otra pintura o un color parecido.");
        }

        var grupos = new Dictionary<string, List<Envase>>();
        foreach (var p in productos)
        {
            var envase = new Envase();
            envase.ProductoId = p.ProductoId;
            envase.Nombre = p.Nombre;
            envase.Litros = (double)p.Litros!.Value;
            envase.Precio = p.Precio;
            envase.Stock = p.Stock;

            string nombreDeLaPintura = NombreSinEnvase(p.Nombre);
            if (!grupos.ContainsKey(nombreDeLaPintura))
            {
                grupos[nombreDeLaPintura] = new List<Envase>();
            }
            grupos[nombreDeLaPintura].Add(envase);
        }

        var opciones = new List<object>();
        foreach (var grupo in grupos)
        {
            var combinacion = Calculos.EnvasesMasBaratos(litros, grupo.Value);
            if (combinacion == null)
            {
                continue;
            }

            var elegidos = new List<object>();
            foreach (var elegido in combinacion.Elegidos)
            {
                elegidos.Add(new { producto_id = elegido.Envase.ProductoId, nombre = elegido.Envase.Nombre, cantidad = elegido.Cantidad });
            }
            opciones.Add(new { pintura = grupo.Key, litros_en_envases = combinacion.Litros, precio_total = combinacion.Precio, envases = elegidos });
        }

        if (opciones.Count == 0)
        {
            return ConError("No hay stock suficiente para cubrir esos litros. Avisa al cliente cuántos litros hay disponibles o deriva a un vendedor.");
        }

        string resultado = JsonSerializer.Serialize(new
        {
            litros_pedidos = litros,
            opciones,
            indicacion = "Cada opción es una sola pintura y un solo color. Si hay más de una, pregunta al cliente cuál prefiere: no las mezcles.",
        }, json);
        return Correcto(resultado);
    }

    public static string NombreSinEnvase(string nombre)
    {
        int parentesis = nombre.IndexOf('(');
        if (parentesis < 0)
        {
            return nombre.Trim();
        }
        return nombre.Substring(0, parentesis).Trim();
    }

    static ResultadoHerramienta CalcularCablesEnTubo(IReadOnlyDictionary<string, JsonElement> datos)
    {
        double seccion = Numero(datos, "seccion_mm2");
        int cables = (int)Numero(datos, "cables");
        int diametro = 0;
        if (datos.ContainsKey("diametro_tubo_mm"))
        {
            diametro = (int)Numero(datos, "diametro_tubo_mm");
        }

        if (!Calculos.SeccionConocida(seccion))
        {
            return ConError("Solo se calcula para cables de 1,5, 2,5 o 4 mm². Para otras secciones, recomienda consultar a un instalador eléctrico autorizado.");
        }
        if (cables < 1 || cables > 50)
        {
            return ConError("La cantidad de cables debe estar entre 1 y 50.");
        }
        if (diametro < 0 || diametro > 100)
        {
            return ConError("El diámetro del tubo debe estar entre 0 y 100 mm.");
        }

        var r = Calculos.CablesEnTubo(seccion, cables, diametro);
        string resultado = JsonSerializer.Serialize(new
        {
            caben = r.Caben,
            diametro_minimo_mm = r.DiametroMinimo,
            maximo_de_cables_en_ese_tubo = r.MaximoEnElTubo,
            tubos_necesarios = r.TubosNecesarios,
            diametro_de_los_tubos_mm = r.DiametroDeLosTubos,
            regla = "Tabla N.º 4.17 del Pliego RIC N.º 04 de la SEC: hasta 5 conductores por tubo. Con más de 5, se reparten en varios tubos. Un diámetro mínimo de 0 significa que no caben en un solo tubo.",
        }, json);
        return Correcto(resultado);
    }

    async Task Derivar(int conversacionId)
    {
        await mensajeria.DerivarAVendedor(conversacionId);
        SeDerivo = true;
    }

    static ResultadoHerramienta Correcto(string texto)
    {
        var resultado = new ResultadoHerramienta { Texto = texto, EsError = false };
        return resultado;
    }

    static ResultadoHerramienta ConError(string texto)
    {
        var resultado = new ResultadoHerramienta { Texto = texto, EsError = true };
        return resultado;
    }

    static double Numero(IReadOnlyDictionary<string, JsonElement> datos, string campo)
    {
        double numero = datos[campo].GetDouble();
        return numero;
    }

    static string Texto(IReadOnlyDictionary<string, JsonElement> datos, string campo)
    {
        string texto = datos[campo].GetString() ?? "";
        return texto;
    }

    static JsonElement Campo(string tipo, string descripcion)
    {
        var campo = JsonSerializer.SerializeToElement(new { type = tipo, description = descripcion });
        return campo;
    }
}
