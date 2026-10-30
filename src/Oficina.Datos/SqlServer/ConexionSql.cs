using System.Data;
using Microsoft.Data.SqlClient;

namespace Oficina.Datos.SqlServer;

// datos para la conexion con sql (la idea es poder probar de que forma podrian conectarse de forma remota a la base de datos. podriamos probar subiendola a un servidor)
public abstract class ConexionSql
{
    private readonly string _cadena;

    protected ConexionSql(string cadenaConexion) => _cadena = cadenaConexion;

    protected SqlConnection Abrir()
    {
        var cn = new SqlConnection(_cadena);
        cn.Open();
        return cn;
    }

    protected static SqlParameter P(string nombre, object valor) => new(nombre, valor ?? DBNull.Value);

    protected List<T> Leer<T>(string sql, Func<SqlDataReader, T> mapear, params SqlParameter[] parametros)
    {
        using var cn = Abrir();
        using var cmd = Comando(cn, null, sql, parametros);
        using var r = cmd.ExecuteReader();
        var lista = new List<T>();
        while (r.Read()) lista.Add(mapear(r));
        return lista;
    }

    protected T LeerUno<T>(string sql, Func<SqlDataReader, T> mapear, params SqlParameter[] parametros) where T : class
        => Leer(sql, mapear, parametros).FirstOrDefault();

    protected object Escalar(string sql, params SqlParameter[] parametros)
    {
        using var cn = Abrir();
        using var cmd = Comando(cn, null, sql, parametros);
        return Traducir(() => cmd.ExecuteScalar());
    }

    protected int Ejecutar(string sql, params SqlParameter[] parametros)
    {
        using var cn = Abrir();
        using var cmd = Comando(cn, null, sql, parametros);
        return Traducir(() => cmd.ExecuteNonQuery());
    }

    // Da de alta y devuelve el Id nuevo (el SQL tiene que terminar en el INSERT)
    protected int Insertar(string sqlInsert, params SqlParameter[] parametros)
        => Convert.ToInt32(Escalar(sqlInsert + "; SELECT CAST(SCOPE_IDENTITY() AS INT);", parametros));

    protected static SqlCommand Comando(SqlConnection cn, SqlTransaction tx, string sql, params SqlParameter[] parametros)
    {
        var cmd = new SqlCommand(sql, cn, tx) { CommandType = CommandType.Text };
        cmd.Parameters.AddRange(parametros);
        return cmd;
    }

    // errores sobre cosas repetidas
    protected static T Traducir<T>(Func<T> accion)
    {
        try { return accion(); }
        catch (SqlException ex) when (ex.Number is 2627 or 2601)
        {
            string m = ex.Message;
            string texto = m.Contains("UQ_Acta_Clave") ? "Ya existe un acta con ese N° de causa, repartición y año."
                : m.Contains("UQ_Reclamo") ? "Ya existe un reclamo con ese N° de caso."
                : m.Contains("UQ_Lote") ? "Ya existe un lote con ese número."
                : m.Contains("UQ_Usuario") ? "Ya existe una cuenta con ese nombre."
                : m.Contains("UQ_Rol") ? "Ya existe un rol con ese nombre."
                : m.Contains("UQ_Sector") ? "Ya existe un sector con ese nombre o clave."
                : m.Contains("UQ_Persona") ? "Ya existe una persona con ese documento."
                : "Ya existe un registro con esos datos.";
            throw new InvalidOperationException(texto, ex);
        }
    }
}

// forma de leer los datos para que sea mas comodo
internal static class LectorExtensiones
{
    public static string Texto(this SqlDataReader r, string c) => r[c] is DBNull ? null : Convert.ToString(r[c]);
    public static int Entero(this SqlDataReader r, string c) => Convert.ToInt32(r[c]);
    public static int? EnteroN(this SqlDataReader r, string c) => r[c] is DBNull ? null : Convert.ToInt32(r[c]);
    public static long Largo(this SqlDataReader r, string c) => Convert.ToInt64(r[c]);
    public static decimal? DecimalN(this SqlDataReader r, string c) => r[c] is DBNull ? null : Convert.ToDecimal(r[c]);
    public static DateTime Fecha(this SqlDataReader r, string c) => Convert.ToDateTime(r[c]);
    public static DateTime? FechaN(this SqlDataReader r, string c) => r[c] is DBNull ? null : Convert.ToDateTime(r[c]);
    public static bool Logico(this SqlDataReader r, string c) => Convert.ToBoolean(r[c]);
    public static byte[] Bytes(this SqlDataReader r, string c) => (byte[])r[c];
}
