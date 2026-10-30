using System.Globalization;

namespace Oficina.Datos;

//Compara texto como lo hace la base (Modern_Spanish_CI_AI): sin mayúsculas ni acentos
public static class TextoBusqueda
{
    private static readonly CompareInfo Comparador = new CultureInfo("es-AR").CompareInfo;
    private const CompareOptions Opciones = CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace;

    public static bool Contiene(string donde, string que)
    {
        if (string.IsNullOrEmpty(que)) return true;
        return !string.IsNullOrEmpty(donde) && Comparador.IndexOf(donde, que, Opciones) >= 0;
    }

    /// Verdadero si alguno de los campos contiene el texto
    public static bool AlgunoContiene(string que, params string[] campos) => campos.Any(c => Contiene(c, que));

    public static bool Iguales(string a, string b) => Comparador.Compare(a ?? "", b ?? "", Opciones) == 0;
}
