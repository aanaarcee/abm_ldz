using Microsoft.Data.SqlClient;
using Oficina.Entidades;

namespace Oficina.Datos.SqlServer;

public class SqlDatosSeguridad : ConexionSql, IDatosSeguridad
{
    public SqlDatosSeguridad(string cadenaConexion) : base(cadenaConexion) { }

    public bool HayUsuarios() => Convert.ToInt32(Escalar("SELECT COUNT(*) FROM Usuario")) > 0;

    // sectores

    private static Sector MapSector(SqlDataReader r) => new()
    {
        IdSector = r.Entero("IdSector"), Nombre = r.Texto("Nombre"), Clave = r.Texto("Clave"), Tipo = (TipoSector)r.Entero("Tipo")
    };

    public List<Sector> ListarSectores() => Leer("SELECT * FROM Sector ORDER BY IdSector", MapSector);

    public void GuardarSector(Sector sector)
    {
        var ps = new[] { P("@Nombre", sector.Nombre), P("@Clave", sector.Clave), P("@Tipo", (byte)sector.Tipo) };
        if (sector.IdSector == 0)
            sector.IdSector = Insertar("INSERT INTO Sector (Nombre, Clave, Tipo) VALUES (@Nombre, @Clave, @Tipo)", ps);
        else
            Ejecutar("UPDATE Sector SET Nombre = @Nombre, Clave = @Clave, Tipo = @Tipo WHERE IdSector = @Id", [.. ps, P("@Id", sector.IdSector)]);
    }

    // roles

    private static Rol MapRol(SqlDataReader r) => new()
    {
        IdRol = r.Entero("IdRol"), Nombre = r.Texto("Nombre"), IdSectorPropio = r.EnteroN("IdSectorPropio"), EsAdministrador = r.Logico("EsAdministrador")
    };

    public List<Rol> ListarRoles() => Leer("SELECT * FROM Rol ORDER BY IdRol", MapRol);

    public Rol ObtenerRol(int idRol) => LeerUno("SELECT * FROM Rol WHERE IdRol = @Id", MapRol, P("@Id", idRol));

    public void GuardarRol(Rol rol)
    {
        var ps = new[] { P("@Nombre", rol.Nombre), P("@Sector", rol.IdSectorPropio), P("@Admin", rol.EsAdministrador) };
        if (rol.IdRol == 0)
            rol.IdRol = Insertar("INSERT INTO Rol (Nombre, IdSectorPropio, EsAdministrador) VALUES (@Nombre, @Sector, @Admin)", ps);
        else
            Ejecutar("UPDATE Rol SET Nombre = @Nombre, IdSectorPropio = @Sector, EsAdministrador = @Admin WHERE IdRol = @Id", [.. ps, P("@Id", rol.IdRol)]);
    }

    // usuarios

    private static Usuario MapUsuario(SqlDataReader r) => new()
    {
        IdUsuario = r.Entero("IdUsuario"), NombreUsuario = r.Texto("NombreUsuario"), ClaveHash = r.Bytes("ClaveHash"),
        ClaveSal = r.Bytes("ClaveSal"), IdRol = r.Entero("IdRol"), Activo = r.Logico("Activo")
    };

    public List<Usuario> ListarUsuarios() => Leer("SELECT * FROM Usuario ORDER BY NombreUsuario", MapUsuario);

    public Usuario ObtenerUsuario(string nombreUsuario)
        => LeerUno("SELECT * FROM Usuario WHERE NombreUsuario = @Nombre", MapUsuario, P("@Nombre", nombreUsuario));

    public void GuardarUsuario(Usuario usuario)
    {
        var ps = new[] { P("@Nombre", usuario.NombreUsuario), P("@Hash", usuario.ClaveHash), P("@Sal", usuario.ClaveSal),
                         P("@Rol", usuario.IdRol), P("@Activo", usuario.Activo) };
        if (usuario.IdUsuario == 0)
            usuario.IdUsuario = Insertar("INSERT INTO Usuario (NombreUsuario, ClaveHash, ClaveSal, IdRol, Activo) VALUES (@Nombre, @Hash, @Sal, @Rol, @Activo)", ps);
        else
            Ejecutar("""
                UPDATE Usuario SET NombreUsuario = @Nombre, ClaveHash = @Hash, ClaveSal = @Sal, IdRol = @Rol, Activo = @Activo
                WHERE IdUsuario = @Id
                """, [.. ps, P("@Id", usuario.IdUsuario)]);
    }

    // permisos
    public List<Permiso> ListarPermisos(int idRol) => Leer("SELECT * FROM Permiso WHERE IdRol = @Rol", r => new Permiso
    {
        IdRol = r.Entero("IdRol"),
        IdSector = r.Entero("IdSector"),
        Acciones = (r.Logico("PuedeLeer") ? Accion.Leer : 0) | (r.Logico("PuedeCargar") ? Accion.Cargar : 0)
                 | (r.Logico("PuedeModificar") ? Accion.Modificar : 0) | (r.Logico("PuedeEliminar") ? Accion.Eliminar : 0)
    }, P("@Rol", idRol));

    public void GuardarPermisos(int idRol, IEnumerable<Permiso> permisos)
    {
        using var cn = Abrir();
        using var tx = cn.BeginTransaction();
        using (var borrar = Comando(cn, tx, "DELETE FROM Permiso WHERE IdRol = @Rol", P("@Rol", idRol)))
            borrar.ExecuteNonQuery();
        foreach (var p in permisos.Where(p => p.Acciones != Accion.Ninguna))
        {
            using var cmd = Comando(cn, tx, """
                INSERT INTO Permiso (IdRol, IdSector, PuedeLeer, PuedeCargar, PuedeModificar, PuedeEliminar)
                VALUES (@Rol, @Sector, @L, @C, @M, @E)
                """,
                P("@Rol", idRol), P("@Sector", p.IdSector), P("@L", p.Puede(Accion.Leer)), P("@C", p.Puede(Accion.Cargar)),
                P("@M", p.Puede(Accion.Modificar)), P("@E", p.Puede(Accion.Eliminar)));
            cmd.ExecuteNonQuery();
        }
        tx.Commit();
    }
}
