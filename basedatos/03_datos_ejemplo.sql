INSERT INTO Clientes (Telefono, Nombre) VALUES (N'56900000001', N'Camila Rojas'), (N'56900000002', N'Pedro Soto');

DECLARE @camila INT = (SELECT ClienteId FROM Clientes WHERE Telefono = N'56900000001');
DECLARE @pedro INT = (SELECT ClienteId FROM Clientes WHERE Telefono = N'56900000002');
DECLARE @vendedor INT = (SELECT UsuarioId FROM Usuarios WHERE Correo = N'vendedor@ferrelupe.cl');

INSERT INTO Conversaciones (ClienteId, Estado, FechaInicio) VALUES (@pedro, N'Asistente', DATEADD(MINUTE, -30, SYSUTCDATETIME()));
DECLARE @conversacionPedro INT = SCOPE_IDENTITY();

INSERT INTO Mensajes (ConversacionId, Emisor, IdWhatsApp, Texto, Fecha) VALUES
    (@conversacionPedro, N'Cliente', N'ejemplo-1', N'Hola, necesito cablear una pieza para poner 10 enchufes', DATEADD(MINUTE, -30, SYSUTCDATETIME())),
    (@conversacionPedro, N'Asistente', NULL, N'¡Hola! Soy el asistente de FerreLupe. Guardamos esta conversación solo para atender tu pedido. ¿Cuánto mide la habitación (largo, ancho y alto) y a qué distancia está el tablero eléctrico?', DATEADD(MINUTE, -29, SYSUTCDATETIME())),
    (@conversacionPedro, N'Cliente', N'ejemplo-2', N'Mide 5x2, alto 2,4 y el tablero está a 5 metros', DATEADD(MINUTE, -28, SYSUTCDATETIME()));

INSERT INTO Conversaciones (ClienteId, UsuarioId, Estado, FechaInicio) VALUES (@camila, @vendedor, N'Vendedor', DATEADD(HOUR, -2, SYSUTCDATETIME()));
DECLARE @conversacionCamila INT = SCOPE_IDENTITY();

INSERT INTO Mensajes (ConversacionId, Emisor, UsuarioId, IdWhatsApp, Texto, Fecha) VALUES
    (@conversacionCamila, N'Cliente', NULL, N'ejemplo-3', N'Quiero pintar mi pieza de blanco, mide 3x4 y tiene 2,4 de alto', DATEADD(MINUTE, -120, SYSUTCDATETIME())),
    (@conversacionCamila, N'Cliente', NULL, N'ejemplo-4', N'¿Me hacen algún descuento si llevo todo?', DATEADD(MINUTE, -110, SYSUTCDATETIME())),
    (@conversacionCamila, N'Asistente', NULL, NULL, N'Eso lo ve directamente un vendedor. En un momento te atiende.', DATEADD(MINUTE, -110, SYSUTCDATETIME())),
    (@conversacionCamila, N'Vendedor', @vendedor, NULL, N'Hola, Camila. Si retiras hoy, te dejamos la bandeja de regalo.', DATEADD(MINUTE, -100, SYSUTCDATETIME()));

INSERT INTO Cotizaciones (ClienteId, ConversacionId, Estado) VALUES (@camila, @conversacionCamila, N'Aceptada');
DECLARE @cotizacion INT = SCOPE_IDENTITY();

INSERT INTO DetallesCotizacion (CotizacionId, ProductoId, Cantidad, PrecioUnitario)
SELECT @cotizacion, ProductoId, d.Cantidad, Precio
FROM (VALUES (N'PIN-001', 3), (N'PIN-003', 1), (N'PIN-004', 1), (N'PIN-005', 1), (N'PIN-006', 1)) AS d (Codigo, Cantidad)
JOIN Productos p ON p.Codigo = d.Codigo;

INSERT INTO Mensajes (ConversacionId, Emisor, UsuarioId, Texto, Fecha, CotizacionId) VALUES
    (@conversacionCamila, N'Vendedor', @vendedor, N'Se envió en PDF: Cotización N.º ' + CAST(@cotizacion AS NVARCHAR(10)) + N' de FerreLupe, con la pintura y los accesorios para tu pieza.', DATEADD(MINUTE, -95, SYSUTCDATETIME()), @cotizacion);
