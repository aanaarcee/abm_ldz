using Oficina.Negocio;
using Oficina.UI.Formularios;

namespace Oficina.UI;

internal static class Program
{
    // Sin Database: se usa OficinaRegistros, y si no existe la aplicación la crea
    private const string CadenaPorDefecto = "Server=.;Trusted_Connection=True;TrustServerCertificate=True";

    [STAThread]
    private static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Application.ThreadException += (_, e) => Dialogo.MostrarError(e.Exception);

        var app = args.Contains("--memoria") ? Aplicacion.EnMemoria() : Aplicacion.ConSqlServer(LeerCadena());
        try
        {
            Cursor.Current = Cursors.WaitCursor;
            if (app.Preparar())
                Dialogo.Aviso((app.BaseCreada ? "No existía la base OficinaRegistros: se creó" : "La base estaba vacía") +
                              " y se cargaron los datos de ejemplo.\n\n" +
                              $"Cuentas: admin / {DatosDeEjemplo.ClaveAdministrador}, y una por sector " +
                              $"(entrada, seguimiento, sala, sentencias, apremio, lotes, reclamos, estadisticas) / {DatosDeEjemplo.ClaveSectores}.");
        }
        catch (Exception ex)
        {
            var r = MessageBox.Show($"No se pudo conectar con la base de datos ({app.Origen}).\n\n{ex.Message}\n\n" +
                                    "¿Abrir en modo prueba? Los datos quedan en memoria y se pierden al cerrar.",
                                    "Registros de Oficina", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
            if (r != DialogResult.Yes) return;
            app = Aplicacion.EnMemoria();
            app.Preparar();
        }
        finally { Cursor.Current = Cursors.Default; }

        while (true)
        {
            Sesion sesion;
            using (var login = new FormLogin(app))
            {
                if (login.ShowDialog() != DialogResult.OK) return;
                sesion = login.Sesion;
            }
            using var principal = new FormPrincipal(app, sesion);
            principal.ShowDialog();
            if (!principal.CambiarDeCuenta) return;
        }
    }

    // lee la cadena de conoxion
    private static string LeerCadena()
    {
        var ruta = Path.Combine(AppContext.BaseDirectory, "conexion.txt");
        var texto = File.Exists(ruta) ? File.ReadAllText(ruta).Trim() : "";
        return texto == "" ? CadenaPorDefecto : texto;
    }
}
