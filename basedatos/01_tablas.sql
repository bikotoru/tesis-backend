CREATE TABLE Roles (
    RolId  INT IDENTITY(1,1) CONSTRAINT PK_Roles PRIMARY KEY,
    Nombre NVARCHAR(30) NOT NULL CONSTRAINT UQ_Roles_Nombre UNIQUE
);

CREATE TABLE Usuarios (
    UsuarioId     INT IDENTITY(1,1) CONSTRAINT PK_Usuarios PRIMARY KEY,
    RolId         INT NOT NULL CONSTRAINT FK_Usuarios_Roles REFERENCES Roles(RolId),
    Nombre        NVARCHAR(100) NOT NULL,
    Correo        NVARCHAR(150) NOT NULL CONSTRAINT UQ_Usuarios_Correo UNIQUE,
    ClaveHash     NVARCHAR(200) NOT NULL,
    Activo        BIT NOT NULL CONSTRAINT DF_Usuarios_Activo DEFAULT 1,
    FechaRegistro DATETIME2 NOT NULL CONSTRAINT DF_Usuarios_Fecha DEFAULT SYSUTCDATETIME()
);

CREATE TABLE Clientes (
    ClienteId     INT IDENTITY(1,1) CONSTRAINT PK_Clientes PRIMARY KEY,
    Telefono      NVARCHAR(20) NOT NULL CONSTRAINT UQ_Clientes_Telefono UNIQUE,
    Nombre        NVARCHAR(100) NULL,
    EsPrueba      BIT NOT NULL CONSTRAINT DF_Clientes_EsPrueba DEFAULT 0,
    FechaRegistro DATETIME2 NOT NULL CONSTRAINT DF_Clientes_Fecha DEFAULT SYSUTCDATETIME()
);

CREATE TABLE Categorias (
    CategoriaId INT IDENTITY(1,1) CONSTRAINT PK_Categorias PRIMARY KEY,
    Nombre      NVARCHAR(60) NOT NULL CONSTRAINT UQ_Categorias_Nombre UNIQUE
);

CREATE TABLE Productos (
    ProductoId  INT IDENTITY(1,1) CONSTRAINT PK_Productos PRIMARY KEY,
    CategoriaId INT NOT NULL CONSTRAINT FK_Productos_Categorias REFERENCES Categorias(CategoriaId),
    Codigo      NVARCHAR(30) NOT NULL CONSTRAINT UQ_Productos_Codigo UNIQUE,
    Nombre      NVARCHAR(150) COLLATE Modern_Spanish_CI_AI NOT NULL,
    Descripcion NVARCHAR(1000) COLLATE Modern_Spanish_CI_AI NULL,
    Unidad      NVARCHAR(20) NOT NULL,
    Precio      INT NOT NULL CONSTRAINT CK_Productos_Precio CHECK (Precio >= 0),
    Stock       INT NOT NULL CONSTRAINT CK_Productos_Stock CHECK (Stock >= 0),
    Litros      DECIMAL(6,3) NULL CONSTRAINT CK_Productos_Litros CHECK (Litros > 0),
    Activo      BIT NOT NULL CONSTRAINT DF_Productos_Activo DEFAULT 1
);

CREATE TABLE Conversaciones (
    ConversacionId INT IDENTITY(1,1) CONSTRAINT PK_Conversaciones PRIMARY KEY,
    ClienteId      INT NOT NULL CONSTRAINT FK_Conversaciones_Clientes REFERENCES Clientes(ClienteId),
    UsuarioId      INT NULL CONSTRAINT FK_Conversaciones_Usuarios REFERENCES Usuarios(UsuarioId),
    Estado         NVARCHAR(10) NOT NULL CONSTRAINT CK_Conversaciones_Estado CHECK (Estado IN ('Asistente', 'Vendedor')),
    FechaInicio    DATETIME2 NOT NULL CONSTRAINT DF_Conversaciones_Fecha DEFAULT SYSUTCDATETIME()
);

CREATE TABLE Mensajes (
    MensajeId      INT IDENTITY(1,1) CONSTRAINT PK_Mensajes PRIMARY KEY,
    ConversacionId INT NOT NULL CONSTRAINT FK_Mensajes_Conversaciones REFERENCES Conversaciones(ConversacionId),
    Emisor         NVARCHAR(10) NOT NULL CONSTRAINT CK_Mensajes_Emisor CHECK (Emisor IN ('Cliente', 'Asistente', 'Vendedor')),
    UsuarioId      INT NULL CONSTRAINT FK_Mensajes_Usuarios REFERENCES Usuarios(UsuarioId),
    IdWhatsApp     NVARCHAR(100) NULL,
    IdImagen       NVARCHAR(100) NULL,
    Texto          NVARCHAR(MAX) NOT NULL,
    Fecha          DATETIME2 NOT NULL CONSTRAINT DF_Mensajes_Fecha DEFAULT SYSUTCDATETIME()
);

CREATE UNIQUE INDEX UQ_Mensajes_IdWhatsApp ON Mensajes(IdWhatsApp) WHERE IdWhatsApp IS NOT NULL;

CREATE INDEX IX_Mensajes_Conversacion ON Mensajes(ConversacionId, Fecha);
CREATE INDEX IX_Conversaciones_Cliente ON Conversaciones(ClienteId);

CREATE TABLE Cotizaciones (
    CotizacionId   INT IDENTITY(1001,1) CONSTRAINT PK_Cotizaciones PRIMARY KEY,
    ClienteId      INT NOT NULL CONSTRAINT FK_Cotizaciones_Clientes REFERENCES Clientes(ClienteId),
    ConversacionId INT NULL CONSTRAINT FK_Cotizaciones_Conversaciones REFERENCES Conversaciones(ConversacionId),
    Fecha          DATETIME2 NOT NULL CONSTRAINT DF_Cotizaciones_Fecha DEFAULT SYSUTCDATETIME(),
    Estado         NVARCHAR(10) NOT NULL CONSTRAINT CK_Cotizaciones_Estado CHECK (Estado IN ('Enviada', 'Aceptada', 'Rechazada'))
);

CREATE INDEX IX_Cotizaciones_Cliente ON Cotizaciones(ClienteId);

ALTER TABLE Mensajes ADD CotizacionId INT NULL CONSTRAINT FK_Mensajes_Cotizaciones REFERENCES Cotizaciones(CotizacionId);

CREATE TABLE Adjuntos (
    AdjuntoId INT IDENTITY(1,1) CONSTRAINT PK_Adjuntos PRIMARY KEY,
    MensajeId INT NOT NULL CONSTRAINT FK_Adjuntos_Mensajes REFERENCES Mensajes(MensajeId) CONSTRAINT UQ_Adjuntos_Mensaje UNIQUE,
    Tipo      NVARCHAR(20) NOT NULL CONSTRAINT CK_Adjuntos_Tipo CHECK (Tipo IN ('image/jpeg', 'image/png')),
    Datos     VARBINARY(MAX) NOT NULL
);

CREATE TABLE DetallesCotizacion (
    CotizacionId   INT NOT NULL CONSTRAINT FK_Detalles_Cotizaciones REFERENCES Cotizaciones(CotizacionId) ON DELETE CASCADE,
    ProductoId     INT NOT NULL CONSTRAINT FK_Detalles_Productos REFERENCES Productos(ProductoId),
    Cantidad       INT NOT NULL CONSTRAINT CK_Detalles_Cantidad CHECK (Cantidad > 0),
    PrecioUnitario INT NOT NULL CONSTRAINT CK_Detalles_Precio CHECK (PrecioUnitario >= 0),
    CONSTRAINT PK_DetallesCotizacion PRIMARY KEY (CotizacionId, ProductoId)
);

CREATE TABLE Configuracion (
    ConfiguracionId      INT NOT NULL CONSTRAINT PK_Configuracion PRIMARY KEY CONSTRAINT CK_Configuracion_UnaFila CHECK (ConfiguracionId = 1),
    MensajeBienvenida    NVARCHAR(500) NOT NULL,
    Horario              NVARCHAR(100) NOT NULL,
    RendimientoPintura   DECIMAL(4,1) NOT NULL CONSTRAINT CK_Configuracion_Rendimiento CHECK (RendimientoPintura > 0),
    MargenMedidas        INT NOT NULL CONSTRAINT CK_Configuracion_Margen CHECK (MargenMedidas BETWEEN 0 AND 50),
    UltimaSincronizacion DATETIME2 NULL
);
