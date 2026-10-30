/*
  Base de datos de Registros de Oficina · prototipo
  ---------------------------------------------------
  Crea la base OficinaRegistros con sus tablas y el procedimiento de búsqueda.
  Los datos de ejemplo (sectores, roles, cuentas, actas) los carga la aplicación
  la primera vez que se conecta a una base vacía.

  Lo que está confirmado por la oficina:
  - Un acta se identifica por N° de causa + repartición + año (el N° de acta se puede repetir).
  - Existe el acta original y el acta modificada: cada cambio guarda una versión.
  - Las actas se guardan al menos 10 años: nunca se borra, solo se marca como eliminada.
  - Reclamos son avisos de autos abandonados en la vía pública; "verificado" es cuando alguien va a ver el auto.

  Lo que todavía es provisorio (se ajusta con las próximas entrevistas):
  - Movimiento.Detalle es texto libre hasta tener la lista real de movimientos.
  - Monto1 y Monto2 todavía no tienen nombre definitivo.
  - Lote guarda cantidades por tipo (tránsito, competencia, prescriptas) hasta saber si agrupa actas.

  La intercalación Modern_Spanish_CI_AI hace que las búsquedas ignoren mayúsculas y acentos.
*/

IF DB_ID(N'OficinaRegistros') IS NULL
    CREATE DATABASE OficinaRegistros COLLATE Modern_Spanish_CI_AI;
GO
USE OficinaRegistros;
GO
-- Necesario para los índices filtrados (sqlcmd los trae apagados)
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

/* ---------- Organización ---------- */

CREATE TABLE Sector (
    IdSector        INT IDENTITY PRIMARY KEY,
    Nombre          NVARCHAR(60)  NOT NULL CONSTRAINT UQ_Sector_Nombre UNIQUE,
    Clave           NVARCHAR(4)   NOT NULL CONSTRAINT UQ_Sector_Clave UNIQUE,
    -- Qué tipo de filas lleva el sector: 0 Consulta (no carga), 1 Movimientos de actas, 2 Reclamos, 3 Lotes
    Tipo            TINYINT       NOT NULL CONSTRAINT CK_Sector_Tipo CHECK (Tipo BETWEEN 0 AND 3)
);

CREATE TABLE Rol (
    IdRol           INT IDENTITY PRIMARY KEY,
    Nombre          NVARCHAR(60)  NOT NULL CONSTRAINT UQ_Rol_Nombre UNIQUE,
    IdSectorPropio  INT           NULL REFERENCES Sector(IdSector),
    EsAdministrador BIT           NOT NULL DEFAULT 0,
    -- El Administrador es una cuenta aparte: un rol de sector nunca puede serlo
    CONSTRAINT CK_Rol_AdminSinSector CHECK (EsAdministrador = 0 OR IdSectorPropio IS NULL)
);

-- Una cuenta general por sector. La clave se guarda como hash PBKDF2 con sal.
CREATE TABLE Usuario (
    IdUsuario       INT IDENTITY PRIMARY KEY,
    NombreUsuario   VARCHAR(30)   NOT NULL CONSTRAINT UQ_Usuario_Nombre UNIQUE,
    ClaveHash       VARBINARY(32) NOT NULL,
    ClaveSal        VARBINARY(16) NOT NULL,
    IdRol           INT           NOT NULL REFERENCES Rol(IdRol),
    Activo          BIT           NOT NULL DEFAULT 1
);

-- Qué puede hacer cada rol en cada sector. El Administrador no necesita filas: puede todo.
CREATE TABLE Permiso (
    IdRol           INT NOT NULL REFERENCES Rol(IdRol) ON DELETE CASCADE,
    IdSector        INT NOT NULL REFERENCES Sector(IdSector),
    PuedeLeer       BIT NOT NULL DEFAULT 0,
    PuedeCargar     BIT NOT NULL DEFAULT 0,
    PuedeModificar  BIT NOT NULL DEFAULT 0,
    PuedeEliminar   BIT NOT NULL DEFAULT 0,
    CONSTRAINT PK_Permiso PRIMARY KEY (IdRol, IdSector)
);

/* ---------- Listas fijas ---------- */

CREATE TABLE Reparticion (
    IdReparticion   INT IDENTITY PRIMARY KEY,
    Nombre          NVARCHAR(80) NOT NULL CONSTRAINT UQ_Reparticion UNIQUE
);

CREATE TABLE Juzgado (
    IdJuzgado       INT IDENTITY PRIMARY KEY,
    Nombre          NVARCHAR(80) NOT NULL CONSTRAINT UQ_Juzgado UNIQUE
);

/* ---------- Personas ---------- */

CREATE TABLE Persona (
    IdPersona       INT IDENTITY PRIMARY KEY,
    Apellido        NVARCHAR(80)  NOT NULL,   -- o razón social
    Nombre          NVARCHAR(80)  NULL,
    TipoDocumento   VARCHAR(4)    NOT NULL DEFAULT 'DNI' CONSTRAINT CK_Persona_TipoDoc CHECK (TipoDocumento IN ('DNI','CUIT')),
    NroDocumento    VARCHAR(13)   NULL        -- solo números; puede faltar
);
CREATE UNIQUE INDEX UQ_Persona_Documento ON Persona(TipoDocumento, NroDocumento) WHERE NroDocumento IS NOT NULL;
CREATE INDEX IX_Persona_Apellido ON Persona(Apellido);

/* ---------- Actas ---------- */

CREATE TABLE Acta (
    IdActa          INT IDENTITY PRIMARY KEY,
    NroCausa        VARCHAR(20)   NOT NULL,
    IdReparticion   INT           NOT NULL REFERENCES Reparticion(IdReparticion),
    Anio            SMALLINT      NOT NULL,
    NroActa         VARCHAR(20)   NOT NULL,
    IdJuzgado       INT           NULL REFERENCES Juzgado(IdJuzgado),
    IdPersona       INT           NULL REFERENCES Persona(IdPersona),
    FechaActa       DATE          NOT NULL,
    Monto1          DECIMAL(12,2) NULL,       -- nombre definitivo a confirmar
    Monto2          DECIMAL(12,2) NULL,       -- nombre definitivo a confirmar
    Observaciones   NVARCHAR(500) NULL,
    Version         INT           NOT NULL DEFAULT 1,
    Eliminado       BIT           NOT NULL DEFAULT 0,
    IdUsuarioAlta   INT           NOT NULL REFERENCES Usuario(IdUsuario),
    FechaAlta       DATETIME2(0)  NOT NULL DEFAULT SYSDATETIME(),
    IdUsuarioModif  INT           NULL REFERENCES Usuario(IdUsuario),
    FechaModif      DATETIME2(0)  NULL,
    -- Lo que hace única a un acta, según la oficina
    CONSTRAINT UQ_Acta_Clave UNIQUE (NroCausa, IdReparticion, Anio)
);
CREATE INDEX IX_Acta_NroActa ON Acta(NroActa);
CREATE INDEX IX_Acta_Persona ON Acta(IdPersona);

-- Copia de cada versión del acta. La versión 1 es el acta original.
CREATE TABLE ActaVersion (
    IdActaVersion   INT IDENTITY PRIMARY KEY,
    IdActa          INT           NOT NULL REFERENCES Acta(IdActa),
    NroVersion      INT           NOT NULL,
    NroActa         VARCHAR(20)   NOT NULL,
    IdJuzgado       INT           NULL,
    IdPersona       INT           NULL,
    FechaActa       DATE          NOT NULL,
    Monto1          DECIMAL(12,2) NULL,
    Monto2          DECIMAL(12,2) NULL,
    Observaciones   NVARCHAR(500) NULL,
    Motivo          NVARCHAR(200) NULL,
    IdUsuario       INT           NOT NULL REFERENCES Usuario(IdUsuario),
    Fecha           DATETIME2(0)  NOT NULL DEFAULT SYSDATETIME(),
    CONSTRAINT UQ_ActaVersion UNIQUE (IdActa, NroVersion)
);

-- Lo que cada sector anota sobre un acta (hoy, una fila en su planilla).
CREATE TABLE Movimiento (
    IdMovimiento    INT IDENTITY PRIMARY KEY,
    IdActa          INT           NOT NULL REFERENCES Acta(IdActa),
    IdSector        INT           NOT NULL REFERENCES Sector(IdSector),
    Fecha           DATE          NOT NULL,
    Detalle         NVARCHAR(100) NOT NULL,   -- texto libre hasta tener la lista de movimientos
    Remito          VARCHAR(30)   NULL,
    Observaciones   NVARCHAR(500) NULL,
    Eliminado       BIT           NOT NULL DEFAULT 0,
    IdUsuarioAlta   INT           NOT NULL REFERENCES Usuario(IdUsuario),
    FechaAlta       DATETIME2(0)  NOT NULL DEFAULT SYSDATETIME(),
    IdUsuarioModif  INT           NULL REFERENCES Usuario(IdUsuario),
    FechaModif      DATETIME2(0)  NULL
);
CREATE INDEX IX_Movimiento_Sector ON Movimiento(IdSector) WHERE Eliminado = 0;
CREATE INDEX IX_Movimiento_Acta ON Movimiento(IdActa);
CREATE INDEX IX_Movimiento_Remito ON Movimiento(Remito) WHERE Remito IS NOT NULL;

/* ---------- Lotes y reclamos ---------- */

CREATE TABLE Lote (
    IdLote          INT IDENTITY PRIMARY KEY,
    NroLote         VARCHAR(20)   NOT NULL CONSTRAINT UQ_Lote UNIQUE,
    Fecha           DATE          NOT NULL,
    CantTransito    INT           NOT NULL DEFAULT 0,
    CantCompetencia INT           NOT NULL DEFAULT 0,
    CantPrescriptas INT           NOT NULL DEFAULT 0,
    Observaciones   NVARCHAR(500) NULL,
    Eliminado       BIT           NOT NULL DEFAULT 0,
    IdUsuarioAlta   INT           NOT NULL REFERENCES Usuario(IdUsuario),
    FechaAlta       DATETIME2(0)  NOT NULL DEFAULT SYSDATETIME(),
    IdUsuarioModif  INT           NULL REFERENCES Usuario(IdUsuario),
    FechaModif      DATETIME2(0)  NULL
);

-- Avisos de autos abandonados en la vía pública
CREATE TABLE Reclamo (
    IdReclamo         INT IDENTITY PRIMARY KEY,
    NroCaso           VARCHAR(20)   NOT NULL CONSTRAINT UQ_Reclamo UNIQUE,
    Fecha             DATE          NOT NULL,
    Ubicacion         NVARCHAR(150) NOT NULL,
    Vehiculo          NVARCHAR(100) NULL,     -- marca, color, patente si se sabe
    Verificado        BIT           NOT NULL DEFAULT 0,
    FechaVerificacion DATE          NULL,
    VerificadoPor     NVARCHAR(80)  NULL,
    Observaciones     NVARCHAR(500) NULL,
    Eliminado         BIT           NOT NULL DEFAULT 0,
    IdUsuarioAlta     INT           NOT NULL REFERENCES Usuario(IdUsuario),
    FechaAlta         DATETIME2(0)  NOT NULL DEFAULT SYSDATETIME(),
    IdUsuarioModif    INT           NULL REFERENCES Usuario(IdUsuario),
    FechaModif        DATETIME2(0)  NULL
);

/* ---------- Historial de cambios ---------- */

CREATE TABLE Historial (
    IdHistorial     BIGINT IDENTITY PRIMARY KEY,
    Entidad         VARCHAR(20)   NOT NULL,   -- Acta, Movimiento, Reclamo, Lote, Permiso…
    IdEntidad       INT           NOT NULL,
    IdSector        INT           NULL REFERENCES Sector(IdSector),
    Accion          VARCHAR(15)   NOT NULL,   -- Alta, Modificación, Baja
    Detalle         NVARCHAR(400) NULL,
    IdUsuario       INT           NOT NULL REFERENCES Usuario(IdUsuario),
    Fecha           DATETIME2(0)  NOT NULL DEFAULT SYSDATETIME()
);
CREATE INDEX IX_Historial_Entidad ON Historial(Entidad, IdEntidad);
CREATE INDEX IX_Historial_Fecha ON Historial(Fecha DESC);
GO

/* ---------- Búsqueda general ----------
   Busca en actas (causa, acta, persona, documento, observaciones), en lo que anotó cada sector
   (movimiento, remito), en reclamos y en lotes. Solo devuelve filas de los sectores que se le pasan
   (los que el rol puede leer). Con @IdSector busca dentro de un solo sector.
*/
CREATE OR ALTER PROCEDURE sp_BuscarGeneral
    @Texto     NVARCHAR(100),
    @Sectores  VARCHAR(400),      -- ids separados por coma
    @IdSector  INT = NULL
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @t NVARCHAR(102) = N'%' + @Texto + N'%';
    DECLARE @s TABLE (IdSector INT PRIMARY KEY);
    INSERT INTO @s SELECT DISTINCT TRY_CAST(value AS INT) FROM STRING_SPLIT(@Sectores, ',') WHERE TRY_CAST(value AS INT) IS NOT NULL;
    IF @IdSector IS NOT NULL DELETE FROM @s WHERE IdSector <> @IdSector;

    SELECT TOP 300 * FROM (
        -- Actas, una fila por cada sector donde el acta tiene movimientos
        SELECT 'Acta' AS Tipo, s.IdSector, s.Nombre AS Sector, m.IdMovimiento AS Id, a.IdActa,
               CONCAT('Causa ', a.NroCausa, ' · ', r.Nombre, ' · ', a.Anio) AS Titulo,
               CONCAT('Acta ', a.NroActa, ISNULL(' · ' + p.Apellido + ISNULL(', ' + p.Nombre, ''), ''), ' · ', m.Detalle,
                      ISNULL(' · Remito ' + m.Remito, '')) AS Detalle,
               CAST(m.Fecha AS DATETIME2(0)) AS Fecha
        FROM Movimiento m
        JOIN @s f          ON f.IdSector = m.IdSector
        JOIN Sector s      ON s.IdSector = m.IdSector
        JOIN Acta a        ON a.IdActa = m.IdActa
        JOIN Reparticion r ON r.IdReparticion = a.IdReparticion
        LEFT JOIN Persona p ON p.IdPersona = a.IdPersona
        WHERE m.Eliminado = 0 AND a.Eliminado = 0
          AND (a.NroCausa LIKE @t OR a.NroActa LIKE @t OR r.Nombre LIKE @t OR CAST(a.Anio AS VARCHAR(4)) = @Texto
               OR p.Apellido LIKE @t OR p.Nombre LIKE @t OR p.NroDocumento LIKE @t
               OR a.Observaciones LIKE @t OR m.Detalle LIKE @t OR m.Remito LIKE @t OR m.Observaciones LIKE @t)

        UNION ALL
        SELECT 'Reclamo', s.IdSector, s.Nombre, rc.IdReclamo, NULL,
               CONCAT('Caso ', rc.NroCaso),
               CONCAT(rc.Ubicacion, ISNULL(' · ' + rc.Vehiculo, ''), CASE WHEN rc.Verificado = 1 THEN ' · Verificado' ELSE ' · Sin verificar' END),
               CAST(rc.Fecha AS DATETIME2(0))
        FROM Reclamo rc
        JOIN Sector s ON s.Tipo = 2
        JOIN @s f     ON f.IdSector = s.IdSector
        WHERE rc.Eliminado = 0
          AND (rc.NroCaso LIKE @t OR rc.Ubicacion LIKE @t OR rc.Vehiculo LIKE @t OR rc.VerificadoPor LIKE @t OR rc.Observaciones LIKE @t)

        UNION ALL
        SELECT 'Lote', s.IdSector, s.Nombre, l.IdLote, NULL,
               CONCAT('Lote ', l.NroLote),
               CONCAT('Tránsito ', l.CantTransito, ' · Competencia ', l.CantCompetencia, ' · Prescriptas ', l.CantPrescriptas),
               CAST(l.Fecha AS DATETIME2(0))
        FROM Lote l
        JOIN Sector s ON s.Tipo = 3
        JOIN @s f     ON f.IdSector = s.IdSector
        WHERE l.Eliminado = 0
          AND (l.NroLote LIKE @t OR l.Observaciones LIKE @t)
    ) x
    ORDER BY Fecha DESC;
END
GO
