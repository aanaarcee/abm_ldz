using System.Data;
using Microsoft.Data.SqlClient;
using Oficina.Entidades;

namespace Oficina.Datos.SqlServer;

public class SqlDatosRegistros : ConexionSql, IDatosRegistros
{
    public SqlDatosRegistros(string cadenaConexion) : base(cadenaConexion) { }

    private const string NombrePersona =
        "CASE WHEN p.IdPersona IS NULL THEN NULL WHEN ISNULL(p.Nombre, '') = '' THEN p.Apellido ELSE p.Apellido + ', ' + p.Nombre END";

    private static string ListaIds(IEnumerable<int> ids) => string.Join(",", ids);

    private static void Auditoria(RegistroAuditado x, SqlDataReader r)
    {
        x.Eliminado = r.Logico("Eliminado");
        x.IdUsuarioAlta = r.Entero("IdUsuarioAlta");
        x.FechaAlta = r.Fecha("FechaAlta");
        x.IdUsuarioModif = r.EnteroN("IdUsuarioModif");
        x.FechaModif = r.FechaN("FechaModif");
    }

    private static SqlParameter[] ParametrosAuditoria(RegistroAuditado x) =>
    [
        P("@Eliminado", x.Eliminado), P("@UsuarioAlta", x.IdUsuarioAlta), P("@FechaAlta", x.FechaAlta),
        P("@UsuarioModif", x.IdUsuarioModif), P("@FechaModif", x.FechaModif)
    ];

    // listas

    public List<Reparticion> ListarReparticiones() => Leer("SELECT * FROM Reparticion ORDER BY Nombre",
        r => new Reparticion { IdReparticion = r.Entero("IdReparticion"), Nombre = r.Texto("Nombre") });

    public void GuardarReparticion(Reparticion reparticion)
    {
        if (reparticion.IdReparticion == 0)
            reparticion.IdReparticion = Insertar("INSERT INTO Reparticion (Nombre) VALUES (@Nombre)", P("@Nombre", reparticion.Nombre));
        else
            Ejecutar("UPDATE Reparticion SET Nombre = @Nombre WHERE IdReparticion = @Id", P("@Nombre", reparticion.Nombre), P("@Id", reparticion.IdReparticion));
    }

    public List<Juzgado> ListarJuzgados() => Leer("SELECT * FROM Juzgado ORDER BY Nombre",
        r => new Juzgado { IdJuzgado = r.Entero("IdJuzgado"), Nombre = r.Texto("Nombre") });

    public void GuardarJuzgado(Juzgado juzgado)
    {
        if (juzgado.IdJuzgado == 0)
            juzgado.IdJuzgado = Insertar("INSERT INTO Juzgado (Nombre) VALUES (@Nombre)", P("@Nombre", juzgado.Nombre));
        else
            Ejecutar("UPDATE Juzgado SET Nombre = @Nombre WHERE IdJuzgado = @Id", P("@Nombre", juzgado.Nombre), P("@Id", juzgado.IdJuzgado));
    }

    // personas

    private static Persona MapPersona(SqlDataReader r) => new()
    {
        IdPersona = r.Entero("IdPersona"), Apellido = r.Texto("Apellido"), Nombre = r.Texto("Nombre"),
        TipoDocumento = r.Texto("TipoDocumento"), NroDocumento = r.Texto("NroDocumento")
    };

    public Persona ObtenerPersona(int idPersona) => LeerUno("SELECT * FROM Persona WHERE IdPersona = @Id", MapPersona, P("@Id", idPersona));

    public Persona BuscarPersonaPorDocumento(string tipoDocumento, string nroDocumento) =>
        LeerUno("SELECT * FROM Persona WHERE TipoDocumento = @Tipo AND NroDocumento = @Nro", MapPersona, P("@Tipo", tipoDocumento), P("@Nro", nroDocumento));

    public void GuardarPersona(Persona persona)
    {
        var ps = new[] { P("@Apellido", persona.Apellido), P("@Nombre", persona.Nombre), P("@Tipo", persona.TipoDocumento), P("@Nro", persona.NroDocumento) };
        if (persona.IdPersona == 0)
            persona.IdPersona = Insertar("INSERT INTO Persona (Apellido, Nombre, TipoDocumento, NroDocumento) VALUES (@Apellido, @Nombre, @Tipo, @Nro)", ps);
        else
            Ejecutar("UPDATE Persona SET Apellido = @Apellido, Nombre = @Nombre, TipoDocumento = @Tipo, NroDocumento = @Nro WHERE IdPersona = @Id",
                [.. ps, P("@Id", persona.IdPersona)]);
    }

    // actas

    private const string SelectActa = $"""
        SELECT a.*, r.Nombre AS NombreReparticion, j.Nombre AS NombreJuzgado, {NombrePersona} AS NombrePersona, p.NroDocumento AS Documento
        FROM Acta a
        JOIN Reparticion r ON r.IdReparticion = a.IdReparticion
        LEFT JOIN Juzgado j ON j.IdJuzgado = a.IdJuzgado
        LEFT JOIN Persona p ON p.IdPersona = a.IdPersona
        """;

    private static Acta MapActa(SqlDataReader r)
    {
        var a = new Acta
        {
            IdActa = r.Entero("IdActa"), NroCausa = r.Texto("NroCausa"), IdReparticion = r.Entero("IdReparticion"), Anio = r.Entero("Anio"),
            NroActa = r.Texto("NroActa"), IdJuzgado = r.EnteroN("IdJuzgado"), IdPersona = r.EnteroN("IdPersona"), FechaActa = r.Fecha("FechaActa"),
            Monto1 = r.DecimalN("Monto1"), Monto2 = r.DecimalN("Monto2"), Observaciones = r.Texto("Observaciones"), Version = r.Entero("Version"),
            Reparticion = r.Texto("NombreReparticion"), Juzgado = r.Texto("NombreJuzgado"), Persona = r.Texto("NombrePersona"), Documento = r.Texto("Documento")
        };
        Auditoria(a, r);
        return a;
    }

    public Acta ObtenerActa(int idActa) => LeerUno(SelectActa + " WHERE a.IdActa = @Id", MapActa, P("@Id", idActa));

    public Acta BuscarActa(string nroCausa, int idReparticion, int anio) =>
        LeerUno(SelectActa + " WHERE a.NroCausa = @Causa AND a.IdReparticion = @Rep AND a.Anio = @Anio", MapActa,
            P("@Causa", nroCausa), P("@Rep", idReparticion), P("@Anio", anio));

    public void GuardarActa(Acta acta)
    {
        SqlParameter[] ps =
        [
            P("@Causa", acta.NroCausa), P("@Rep", acta.IdReparticion), P("@Anio", acta.Anio), P("@NroActa", acta.NroActa),
            P("@Juzgado", acta.IdJuzgado), P("@Persona", acta.IdPersona), P("@FechaActa", acta.FechaActa.Date),
            P("@Monto1", acta.Monto1), P("@Monto2", acta.Monto2), P("@Obs", acta.Observaciones), P("@Version", acta.Version),
            .. ParametrosAuditoria(acta)
        ];
        if (acta.IdActa == 0)
            acta.IdActa = Insertar("""
                INSERT INTO Acta (NroCausa, IdReparticion, Anio, NroActa, IdJuzgado, IdPersona, FechaActa, Monto1, Monto2, Observaciones,
                                  Version, Eliminado, IdUsuarioAlta, FechaAlta, IdUsuarioModif, FechaModif)
                VALUES (@Causa, @Rep, @Anio, @NroActa, @Juzgado, @Persona, @FechaActa, @Monto1, @Monto2, @Obs,
                        @Version, @Eliminado, @UsuarioAlta, @FechaAlta, @UsuarioModif, @FechaModif)
                """, ps);
        else
            Ejecutar("""
                UPDATE Acta SET NroCausa = @Causa, IdReparticion = @Rep, Anio = @Anio, NroActa = @NroActa, IdJuzgado = @Juzgado,
                    IdPersona = @Persona, FechaActa = @FechaActa, Monto1 = @Monto1, Monto2 = @Monto2, Observaciones = @Obs, Version = @Version,
                    Eliminado = @Eliminado, IdUsuarioModif = @UsuarioModif, FechaModif = @FechaModif
                WHERE IdActa = @Id
                """, [.. ps, P("@Id", acta.IdActa)]);
    }

    public List<ActaVersion> ListarVersiones(int idActa) => Leer($"""
        SELECT v.*, j.Nombre AS NombreJuzgado, {NombrePersona} AS NombrePersona
        FROM ActaVersion v
        LEFT JOIN Juzgado j ON j.IdJuzgado = v.IdJuzgado
        LEFT JOIN Persona p ON p.IdPersona = v.IdPersona
        WHERE v.IdActa = @Id ORDER BY v.NroVersion
        """, r => new ActaVersion
    {
        IdActaVersion = r.Entero("IdActaVersion"), IdActa = r.Entero("IdActa"), NroVersion = r.Entero("NroVersion"), NroActa = r.Texto("NroActa"),
        IdJuzgado = r.EnteroN("IdJuzgado"), IdPersona = r.EnteroN("IdPersona"), FechaActa = r.Fecha("FechaActa"), Monto1 = r.DecimalN("Monto1"),
        Monto2 = r.DecimalN("Monto2"), Observaciones = r.Texto("Observaciones"), Motivo = r.Texto("Motivo"), IdUsuario = r.Entero("IdUsuario"),
        Fecha = r.Fecha("Fecha"), Juzgado = r.Texto("NombreJuzgado"), Persona = r.Texto("NombrePersona")
    }, P("@Id", idActa));

    public void AgregarVersion(ActaVersion v)
    {
        v.IdActaVersion = Insertar("""
            INSERT INTO ActaVersion (IdActa, NroVersion, NroActa, IdJuzgado, IdPersona, FechaActa, Monto1, Monto2, Observaciones, Motivo, IdUsuario, Fecha)
            VALUES (@Acta, @Nro, @NroActa, @Juzgado, @Persona, @FechaActa, @Monto1, @Monto2, @Obs, @Motivo, @Usuario, @Fecha)
            """,
            P("@Acta", v.IdActa), P("@Nro", v.NroVersion), P("@NroActa", v.NroActa), P("@Juzgado", v.IdJuzgado), P("@Persona", v.IdPersona),
            P("@FechaActa", v.FechaActa.Date), P("@Monto1", v.Monto1), P("@Monto2", v.Monto2), P("@Obs", v.Observaciones),
            P("@Motivo", v.Motivo), P("@Usuario", v.IdUsuario), P("@Fecha", v.Fecha));
    }

    // movimientos

    private const string SelectMovimiento = $"""
        SELECT m.*, s.Nombre AS NombreSector, a.NroCausa, r.Nombre AS NombreReparticion, a.Anio, a.NroActa,
               {NombrePersona} AS NombrePersona, p.NroDocumento AS Documento, a.Monto1, a.Monto2
        FROM Movimiento m
        JOIN Sector s      ON s.IdSector = m.IdSector
        JOIN Acta a        ON a.IdActa = m.IdActa
        JOIN Reparticion r ON r.IdReparticion = a.IdReparticion
        LEFT JOIN Persona p ON p.IdPersona = a.IdPersona
        """;

    private static Movimiento MapMovimiento(SqlDataReader r)
    {
        var m = new Movimiento
        {
            IdMovimiento = r.Entero("IdMovimiento"), IdActa = r.Entero("IdActa"), IdSector = r.Entero("IdSector"), Fecha = r.Fecha("Fecha"),
            Detalle = r.Texto("Detalle"), Remito = r.Texto("Remito"), Observaciones = r.Texto("Observaciones"),
            Sector = r.Texto("NombreSector"), NroCausa = r.Texto("NroCausa"), Reparticion = r.Texto("NombreReparticion"), Anio = r.Entero("Anio"),
            NroActa = r.Texto("NroActa"), Persona = r.Texto("NombrePersona"), Documento = r.Texto("Documento"),
            Monto1 = r.DecimalN("Monto1"), Monto2 = r.DecimalN("Monto2")
        };
        Auditoria(m, r);
        return m;
    }

    public Movimiento ObtenerMovimiento(int idMovimiento) => LeerUno(SelectMovimiento + " WHERE m.IdMovimiento = @Id", MapMovimiento, P("@Id", idMovimiento));

    public List<Movimiento> ListarMovimientos(int idSector) => Leer(SelectMovimiento +
        " WHERE m.IdSector = @Sector AND m.Eliminado = 0 AND a.Eliminado = 0 ORDER BY m.Fecha DESC, m.IdMovimiento DESC",
        MapMovimiento, P("@Sector", idSector));

    public List<Movimiento> ListarMovimientosDeActa(int idActa) => Leer(SelectMovimiento +
        " WHERE m.IdActa = @Acta AND m.Eliminado = 0 ORDER BY m.Fecha, m.IdMovimiento", MapMovimiento, P("@Acta", idActa));

    public void GuardarMovimiento(Movimiento m)
    {
        SqlParameter[] ps =
        [
            P("@Acta", m.IdActa), P("@Sector", m.IdSector), P("@Fecha", m.Fecha.Date), P("@Detalle", m.Detalle),
            P("@Remito", m.Remito), P("@Obs", m.Observaciones), .. ParametrosAuditoria(m)
        ];
        if (m.IdMovimiento == 0)
            m.IdMovimiento = Insertar("""
                INSERT INTO Movimiento (IdActa, IdSector, Fecha, Detalle, Remito, Observaciones, Eliminado, IdUsuarioAlta, FechaAlta, IdUsuarioModif, FechaModif)
                VALUES (@Acta, @Sector, @Fecha, @Detalle, @Remito, @Obs, @Eliminado, @UsuarioAlta, @FechaAlta, @UsuarioModif, @FechaModif)
                """, ps);
        else
            Ejecutar("""
                UPDATE Movimiento SET IdActa = @Acta, IdSector = @Sector, Fecha = @Fecha, Detalle = @Detalle, Remito = @Remito,
                    Observaciones = @Obs, Eliminado = @Eliminado, IdUsuarioModif = @UsuarioModif, FechaModif = @FechaModif
                WHERE IdMovimiento = @Id
                """, [.. ps, P("@Id", m.IdMovimiento)]);
    }

    public Dictionary<int, int> ContarMovimientosPorSector() => Leer("""
        SELECT m.IdSector, COUNT(*) AS Cantidad
        FROM Movimiento m JOIN Acta a ON a.IdActa = m.IdActa
        WHERE m.Eliminado = 0 AND a.Eliminado = 0
        GROUP BY m.IdSector
        """, r => (Sector: r.Entero("IdSector"), Cantidad: r.Entero("Cantidad"))).ToDictionary(x => x.Sector, x => x.Cantidad);

    // reclamos

    private static Reclamo MapReclamo(SqlDataReader r)
    {
        var x = new Reclamo
        {
            IdReclamo = r.Entero("IdReclamo"), NroCaso = r.Texto("NroCaso"), Fecha = r.Fecha("Fecha"), Ubicacion = r.Texto("Ubicacion"),
            Vehiculo = r.Texto("Vehiculo"), Verificado = r.Logico("Verificado"), FechaVerificacion = r.FechaN("FechaVerificacion"),
            VerificadoPor = r.Texto("VerificadoPor"), Observaciones = r.Texto("Observaciones")
        };
        Auditoria(x, r);
        return x;
    }

    public Reclamo ObtenerReclamo(int idReclamo) => LeerUno("SELECT * FROM Reclamo WHERE IdReclamo = @Id", MapReclamo, P("@Id", idReclamo));

    public List<Reclamo> ListarReclamos() => Leer("SELECT * FROM Reclamo WHERE Eliminado = 0 ORDER BY Fecha DESC, IdReclamo DESC", MapReclamo);

    public void GuardarReclamo(Reclamo x)
    {
        SqlParameter[] ps =
        [
            P("@Caso", x.NroCaso), P("@Fecha", x.Fecha.Date), P("@Ubicacion", x.Ubicacion), P("@Vehiculo", x.Vehiculo),
            P("@Verificado", x.Verificado), P("@FechaVerif", x.FechaVerificacion?.Date), P("@VerifPor", x.VerificadoPor),
            P("@Obs", x.Observaciones), .. ParametrosAuditoria(x)
        ];
        if (x.IdReclamo == 0)
            x.IdReclamo = Insertar("""
                INSERT INTO Reclamo (NroCaso, Fecha, Ubicacion, Vehiculo, Verificado, FechaVerificacion, VerificadoPor, Observaciones,
                                     Eliminado, IdUsuarioAlta, FechaAlta, IdUsuarioModif, FechaModif)
                VALUES (@Caso, @Fecha, @Ubicacion, @Vehiculo, @Verificado, @FechaVerif, @VerifPor, @Obs,
                        @Eliminado, @UsuarioAlta, @FechaAlta, @UsuarioModif, @FechaModif)
                """, ps);
        else
            Ejecutar("""
                UPDATE Reclamo SET NroCaso = @Caso, Fecha = @Fecha, Ubicacion = @Ubicacion, Vehiculo = @Vehiculo, Verificado = @Verificado,
                    FechaVerificacion = @FechaVerif, VerificadoPor = @VerifPor, Observaciones = @Obs, Eliminado = @Eliminado,
                    IdUsuarioModif = @UsuarioModif, FechaModif = @FechaModif
                WHERE IdReclamo = @Id
                """, [.. ps, P("@Id", x.IdReclamo)]);
    }

    // lotes

    private static Lote MapLote(SqlDataReader r)
    {
        var x = new Lote
        {
            IdLote = r.Entero("IdLote"), NroLote = r.Texto("NroLote"), Fecha = r.Fecha("Fecha"), CantTransito = r.Entero("CantTransito"),
            CantCompetencia = r.Entero("CantCompetencia"), CantPrescriptas = r.Entero("CantPrescriptas"), Observaciones = r.Texto("Observaciones")
        };
        Auditoria(x, r);
        return x;
    }

    public Lote ObtenerLote(int idLote) => LeerUno("SELECT * FROM Lote WHERE IdLote = @Id", MapLote, P("@Id", idLote));

    public List<Lote> ListarLotes() => Leer("SELECT * FROM Lote WHERE Eliminado = 0 ORDER BY Fecha DESC, IdLote DESC", MapLote);

    public void GuardarLote(Lote x)
    {
        SqlParameter[] ps =
        [
            P("@Nro", x.NroLote), P("@Fecha", x.Fecha.Date), P("@Transito", x.CantTransito), P("@Competencia", x.CantCompetencia),
            P("@Prescriptas", x.CantPrescriptas), P("@Obs", x.Observaciones), .. ParametrosAuditoria(x)
        ];
        if (x.IdLote == 0)
            x.IdLote = Insertar("""
                INSERT INTO Lote (NroLote, Fecha, CantTransito, CantCompetencia, CantPrescriptas, Observaciones,
                                  Eliminado, IdUsuarioAlta, FechaAlta, IdUsuarioModif, FechaModif)
                VALUES (@Nro, @Fecha, @Transito, @Competencia, @Prescriptas, @Obs, @Eliminado, @UsuarioAlta, @FechaAlta, @UsuarioModif, @FechaModif)
                """, ps);
        else
            Ejecutar("""
                UPDATE Lote SET NroLote = @Nro, Fecha = @Fecha, CantTransito = @Transito, CantCompetencia = @Competencia,
                    CantPrescriptas = @Prescriptas, Observaciones = @Obs, Eliminado = @Eliminado, IdUsuarioModif = @UsuarioModif, FechaModif = @FechaModif
                WHERE IdLote = @Id
                """, [.. ps, P("@Id", x.IdLote)]);
    }

    // historial


    private static EntradaHistorial MapHistorial(SqlDataReader r) => new()
    {
        IdHistorial = r.Largo("IdHistorial"), Entidad = r.Texto("Entidad"), IdEntidad = r.Entero("IdEntidad"), IdSector = r.EnteroN("IdSector"),
        Accion = r.Texto("Accion"), Detalle = r.Texto("Detalle"), IdUsuario = r.Entero("IdUsuario"), Fecha = r.Fecha("Fecha"),
        Usuario = r.Texto("NombreUsuario"), Sector = r.Texto("NombreSector")
    };

    private const string SelectHistorial = """
        SELECT h.*, u.NombreUsuario, s.Nombre AS NombreSector
        FROM Historial h
        JOIN Usuario u ON u.IdUsuario = h.IdUsuario
        LEFT JOIN Sector s ON s.IdSector = h.IdSector
        """;

    public void AgregarHistorial(EntradaHistorial h)
    {
        h.IdHistorial = Convert.ToInt64(Escalar("""
            INSERT INTO Historial (Entidad, IdEntidad, IdSector, Accion, Detalle, IdUsuario, Fecha)
            VALUES (@Entidad, @IdEntidad, @Sector, @Accion, @Detalle, @Usuario, @Fecha);
            SELECT CAST(SCOPE_IDENTITY() AS BIGINT);
            """,
            P("@Entidad", h.Entidad), P("@IdEntidad", h.IdEntidad), P("@Sector", h.IdSector), P("@Accion", h.Accion),
            P("@Detalle", Recortar(h.Detalle, 400)), P("@Usuario", h.IdUsuario), P("@Fecha", h.Fecha)));
    }

    private static string Recortar(string s, int largo) => s == null || s.Length <= largo ? s : s[..(largo - 1)] + "…";

    public List<EntradaHistorial> ListarHistorial(string entidad, int idEntidad) => Leer(SelectHistorial +
        " WHERE h.Entidad = @Entidad AND h.IdEntidad = @Id ORDER BY h.Fecha DESC, h.IdHistorial DESC",
        MapHistorial, P("@Entidad", entidad), P("@Id", idEntidad));

    public List<EntradaHistorial> ListarUltimosCambios(IEnumerable<int> sectores, int cantidad) =>
        Leer(SelectHistorial.Replace("SELECT h.*", "SELECT TOP (@Cantidad) h.*") + """
             WHERE h.IdSector IN (SELECT TRY_CAST(value AS INT) FROM STRING_SPLIT(@Sectores, ','))
             ORDER BY h.Fecha DESC, h.IdHistorial DESC
            """, MapHistorial, P("@Cantidad", cantidad), P("@Sectores", ListaIds(sectores)));

    // busqueda

    public List<ResultadoBusqueda> Buscar(string texto, IEnumerable<int> sectores, int? idSector = null)
    {
        using var cn = Abrir();
        using var cmd = new SqlCommand("sp_BuscarGeneral", cn) { CommandType = CommandType.StoredProcedure };
        cmd.Parameters.Add(new SqlParameter("@Texto", SqlDbType.NVarChar, 100) { Value = texto?.Trim() ?? "" });
        cmd.Parameters.Add(new SqlParameter("@Sectores", SqlDbType.VarChar, 400) { Value = ListaIds(sectores) });
        cmd.Parameters.Add(P("@IdSector", idSector));
        using var r = cmd.ExecuteReader();
        var lista = new List<ResultadoBusqueda>();
        while (r.Read())
            lista.Add(new ResultadoBusqueda
            {
                Tipo = r.Texto("Tipo"), IdSector = r.Entero("IdSector"), Sector = r.Texto("Sector"), Id = r.Entero("Id"),
                IdActa = r.EnteroN("IdActa"), Titulo = r.Texto("Titulo"), Detalle = r.Texto("Detalle"), Fecha = r.Fecha("Fecha")
            });
        return lista;
    }
}
