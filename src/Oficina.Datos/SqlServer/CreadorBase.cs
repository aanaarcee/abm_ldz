using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;

namespace Oficina.Datos.SqlServer;


// forma de crear la base de datos si no existe, mas comodo para el git q siempre tiene problemas para las bases

public static class CreadorBase
{
    public const string NombreBase = "OficinaRegistros";

    // Completa la base de datos si la cadena no la indica.
    public static string Completar(string cadenaConexion)
    {
        var b = new SqlConnectionStringBuilder(cadenaConexion);
        if (string.IsNullOrWhiteSpace(b.InitialCatalog)) b.InitialCatalog = NombreBase;
        return b.ConnectionString;
    }

    // va a dar verdadero si se tuvo que crear de 0 la base
    public static bool AsegurarBase(string cadenaConexion)
    {
        var b = new SqlConnectionStringBuilder(Completar(cadenaConexion));
        string nombre = b.InitialCatalog;
        b.InitialCatalog = "master";
        using var cn = new SqlConnection(b.ConnectionString);
        cn.Open();

        using (var cmd = new SqlCommand("SELECT CASE WHEN DB_ID(@Base) IS NULL THEN 0 ELSE 1 END", cn))
        {
            cmd.Parameters.AddWithValue("@Base", nombre);
            if ((int)cmd.ExecuteScalar() == 1)
            {
                cn.ChangeDatabase(nombre);
                using var tablas = new SqlCommand("SELECT COUNT(*) FROM sys.tables WHERE name = 'Sector'", cn);
                if ((int)tablas.ExecuteScalar() > 0) return false;
                cn.ChangeDatabase("master");
            }
        }

        // El script usa el nombre OficinaRegistros: si la cadena pide otro, se reemplaza
        var script = LeerScript().Replace(NombreBase, nombre);
        foreach (var lote in Regex.Split(script, @"^\s*GO\s*$", RegexOptions.Multiline | RegexOptions.IgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(lote)) continue;
            using var cmd = new SqlCommand(lote, cn) { CommandTimeout = 120 };
            cmd.ExecuteNonQuery();
        }
        return true;
    }

    private static string LeerScript()
    {
        using var s = typeof(CreadorBase).Assembly.GetManifestResourceStream("OficinaRegistros.sql")
                      ?? throw new InvalidOperationException("Falta el script OficinaRegistros.sql dentro del programa.");
        using var r = new StreamReader(s);
        return r.ReadToEnd();
    }
}
