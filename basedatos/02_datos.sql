INSERT INTO Roles (Nombre) VALUES (N'Administrador'), (N'Vendedor');

INSERT INTO Usuarios (RolId, Nombre, Correo, ClaveHash) VALUES
    ((SELECT RolId FROM Roles WHERE Nombre = N'Administrador'), N'Dueño de FerreLupe', N'admin@ferrelupe.cl',
     N'AQAAAAIAA1tgAAAAEAlwV+J86hV4VRmMLPjJLAS1cDcnkrJsPoHp6SnLqillHhIymsOnix1dFmLZB2B+MQ=='),
    ((SELECT RolId FROM Roles WHERE Nombre = N'Vendedor'), N'Vendedor de FerreLupe', N'vendedor@ferrelupe.cl',
     N'AQAAAAIAA1tgAAAAEKj5MRpGV6xI7FfnDxIau2QUqM+7J086XqcY+ChaDLbPVEZeRWNppVqKLkVzcInCuA==');

INSERT INTO Categorias (Nombre) VALUES (N'Electricidad'), (N'Pinturas'), (N'Herramientas'), (N'Fijaciones'), (N'Construcción');

INSERT INTO Productos (CategoriaId, Codigo, Nombre, Unidad, Precio, Stock, Litros, Activo, Descripcion)
SELECT c.CategoriaId, p.Codigo, p.Nombre, p.Unidad, p.Precio, p.Stock, p.Litros, p.Activo, p.Descripcion
FROM (VALUES
    (N'Electricidad', N'ELE-001', N'Cable libre de halógenos 2,5 mm² rojo', N'metro', 890, 500, NULL, 1, N'Conductor de cobre de 2,5 mm² con aislación libre de halógenos, color rojo. Se usa como fase en circuitos de enchufes. Se vende por metro.'),
    (N'Electricidad', N'ELE-002', N'Cable libre de halógenos 2,5 mm² blanco', N'metro', 890, 500, NULL, 1, N'Conductor de cobre de 2,5 mm² con aislación libre de halógenos, color blanco. Se usa como neutro en circuitos de enchufes. Se vende por metro.'),
    (N'Electricidad', N'ELE-003', N'Cable libre de halógenos 2,5 mm² verde/amarillo', N'metro', 890, 400, NULL, 1, N'Conductor de cobre de 2,5 mm² con aislación libre de halógenos, color verde y amarillo. Se usa como tierra de protección. Se vende por metro.'),
    (N'Electricidad', N'ELE-004', N'Tubo conduit PVC 20 mm (tira de 3 m)', N'unidad', 1290, 120, NULL, 1, N'Tubo de PVC para canalizar y proteger cables eléctricos en muros y cielos. Tira de 3 metros.'),
    (N'Electricidad', N'ELE-005', N'Caja de derivación PVC', N'unidad', 690, 80, NULL, 1, N'Caja para unir y derivar cables eléctricos en el cielo o en el muro.'),
    (N'Electricidad', N'ELE-006', N'Caja de enchufe PVC para embutir', N'unidad', 490, 90, NULL, 1, NULL),
    (N'Electricidad', N'ELE-007', N'Enchufe doble 10/16 A con tierra', N'unidad', 2990, 60, NULL, 1, N'Enchufe doble con tierra de protección, para instalar en caja de embutir.'),
    (N'Electricidad', N'ELE-008', N'Interruptor automático 1 × 16 A', N'unidad', 4990, 25, NULL, 1, N'Protección termomagnética para circuitos de enchufes. Se instala en el tablero eléctrico.'),
    (N'Electricidad', N'ELE-009', N'Protector diferencial 2 × 25 A 30 mA', N'unidad', 21990, 8, NULL, 1, N'Protege a las personas ante fugas de corriente. Se instala en el tablero eléctrico.'),
    (N'Pinturas', N'PIN-001', N'Pintura látex blanca (galón)', N'galón', 14990, 40, 3.785, 1, N'Pintura al agua para muros y cielos interiores. Rinde unos 10 m² por litro por mano. Lavable y de secado rápido.'),
    (N'Pinturas', N'PIN-002', N'Esmalte al agua blanco (galón)', N'galón', 19990, 25, 3.785, 1, N'Pintura de terminación satinada para muros, puertas y maderas. Resiste la humedad: ideal para baños y cocinas.'),
    (N'Pinturas', N'PIN-007', N'Látex color marfil (galón)', N'galón', 15990, 12, 3.785, 1, N'Pintura al agua color marfil para muros y cielos interiores. Rinde unos 10 m² por litro por mano.'),
    (N'Pinturas', N'PIN-011', N'Látex color marfil (1 litro)', N'litro', 5490, 20, 1, 1, N'Pintura al agua color marfil en envase de 1 litro, para completar la cantidad exacta. Rinde unos 10 m² por litro por mano.'),
    (N'Pinturas', N'PIN-008', N'Látex color marfil (tineta 4 galones)', N'tineta', 54990, 6, 15.14, 1, N'Pintura al agua color marfil en tineta de 4 galones, más económica por litro para trabajos grandes. Rinde unos 10 m² por litro por mano.'),
    (N'Pinturas', N'PIN-009', N'Pintura látex blanca (tineta 4 galones)', N'tineta', 52990, 8, 15.14, 1, N'Pintura al agua blanca en tineta de 4 galones, más económica por litro para trabajos grandes. Rinde unos 10 m² por litro por mano.'),
    (N'Pinturas', N'PIN-010', N'Esmalte al agua blanco (tineta 4 galones)', N'tineta', 68990, 4, 15.14, 1, N'Esmalte al agua blanco en tineta de 4 galones, para muros, puertas y maderas. Resiste la humedad.'),
    (N'Pinturas', N'PIN-003', N'Rodillo antigota 23 cm', N'unidad', 4490, 30, NULL, 1, NULL),
    (N'Pinturas', N'PIN-004', N'Bandeja para pintura', N'unidad', 2490, 30, NULL, 1, NULL),
    (N'Pinturas', N'PIN-005', N'Brocha 2 pulgadas', N'unidad', 1990, 50, NULL, 1, NULL),
    (N'Pinturas', N'PIN-006', N'Cinta de enmascarar 24 mm × 40 m', N'unidad', 1490, 60, NULL, 1, N'Cinta de papel para proteger bordes, marcos y enchufes al pintar. También conocida como huincha de pintor.'),
    (N'Construcción', N'CON-001', N'Adhesivo cerámico (saco 25 kg)', N'saco', 5990, 35, NULL, 1, NULL),
    (N'Fijaciones', N'FIJ-001', N'Tarugo plástico 8 mm (bolsa de 100)', N'bolsa', 2990, 40, NULL, 1, NULL),
    (N'Herramientas', N'HER-001', N'Huincha de medir 5 m', N'unidad', 3990, 20, NULL, 0, NULL)
) AS p (Categoria, Codigo, Nombre, Unidad, Precio, Stock, Litros, Activo, Descripcion)
JOIN Categorias c ON c.Nombre = p.Categoria;

INSERT INTO Configuracion (ConfiguracionId, MensajeBienvenida, Horario, RendimientoPintura, MargenMedidas) VALUES
    (1, N'¡Hola! Soy el asistente de FerreLupe. Guardamos esta conversación solo para atender tu pedido. Cuéntame qué trabajo quieres hacer y te ayudo a calcular los materiales.',
     N'Lunes a sábado, de 9:00 a 19:00', 10, 10);
