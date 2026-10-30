using Oficina.Datos;
using Oficina.Entidades;

namespace Oficina.Negocio;

/// Lo común a los servicios: la sesión, el historial y la traducción de errores de datos
public abstract class ServicioBase
{
    protected Sesion Sesion { get; }
    protected IDatosRegistros Datos { get; }

    protected ServicioBase(Sesion sesion, IDatosRegistros datos)
    {
        Sesion = sesion ?? throw new ArgumentNullException(nameof(sesion));
        Datos = datos;
    }

    protected void Registrar(string entidad, int idEntidad, int? idSector, string accion, string detalle)
        => Datos.AgregarHistorial(new EntradaHistorial
        {
            Entidad = entidad, IdEntidad = idEntidad, IdSector = idSector, Accion = accion, Detalle = detalle,
            IdUsuario = Sesion.IdUsuario, Fecha = DateTime.Now
        });

    /// Ejecuta una escritura y convierte los errores de clave repetida en ExcepcionNegocio 
    protected static void Guardar(Action escritura)
    {
        try { escritura(); }
        catch (InvalidOperationException ex) { throw new ExcepcionNegocio(ex.Message, ex); }
    }

    protected static string Limpio(string s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    protected static void Requerido(string valor, string campo)
    {
        if (string.IsNullOrWhiteSpace(valor)) throw new ExcepcionNegocio($"Falta completar {campo}.");
    }

    protected static void Largo(string valor, int maximo, string campo)
    {
        if (valor != null && valor.Length > maximo) throw new ExcepcionNegocio($"{campo} admite hasta {maximo} caracteres.");
    }
}

// Arma el texto de "qué cambió" para el historial
internal class Cambios
{
    private readonly List<string> _lista = new();

    public Cambios Campo(string nombre, object antes, object despues)
    {
        string a = Formato(antes), d = Formato(despues);
        if (a != d) _lista.Add($"{nombre}: {(a == "" ? "(vacío)" : a)} → {(d == "" ? "(vacío)" : d)}");
        return this;
    }

    public bool Hay => _lista.Count > 0;
    public override string ToString() => string.Join("; ", _lista);

    internal static string Formato(object v) => v switch
    {
        null => "",
        DateTime f => f.ToString("dd/MM/yyyy"),
        decimal m => m.ToString("N2", Formatos.Cultura),
        bool b => b ? "Sí" : "No",
        _ => v.ToString().Trim()
    };
}

public static class Formatos
{
    public static readonly System.Globalization.CultureInfo Cultura = new("es-AR");
}
