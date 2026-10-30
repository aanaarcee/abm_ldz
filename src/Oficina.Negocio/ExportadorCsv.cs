using System.Text;
using Oficina.Datos;
using Oficina.Entidades;

namespace Oficina.Negocio;

// se encarga de exportar la grilla, proximamente deberiamos armar una forma de que puedan cargar datos desde un excel
public class ExportadorCsv : ServicioBase
{
    private readonly ServicioRegistros _registros;

    public ExportadorCsv(Sesion sesion, IDatosRegistros datos) : base(sesion, datos)
        => _registros = new ServicioRegistros(sesion, datos);

    // script para sugerir la carpeta donde descargaria los drives,si no existe no se q hace, pero al menos no tira error
    public static string CarpetaSugerida()
    {
        var perfil = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        string[] candidatas =
        [
            @"G:\Mi unidad", @"G:\My Drive",
            Path.Combine(perfil, "Google Drive", "Mi unidad"), Path.Combine(perfil, "Google Drive"),
            Path.Combine(perfil, "Mi unidad"), Path.Combine(perfil, "My Drive")
        ];
        return candidatas.FirstOrDefault(Directory.Exists) ?? Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
    }

    public static string NombreSugerido(Sector s) => $"{s.Nombre} {DateTime.Now:yyyy-MM-dd HHmm}.csv";

    // Exporta lo que se ve en la grilla del sector (con el mismo filtro). Devuelve la cantidad de filas
    public int ExportarSector(int idSector, string filtro, string ruta)
    {
        var sector = Sesion.Sector(idSector);
        Sesion.Exigir(idSector, Accion.Leer);
        int filas = sector.Tipo switch
        {
            TipoSector.Actas => Escribir(ruta, _registros.Movimientos(idSector, filtro),
                ("Fecha", m => m.Fecha), ("N° de causa", m => m.NroCausa), ("Repartición", m => m.Reparticion), ("Año", m => m.Anio),
                ("N° de acta", m => m.NroActa), ("Persona", m => m.Persona), ("Documento", m => m.Documento),
                ("Movimiento", m => m.Detalle), ("Remito", m => m.Remito), ("Monto 1", m => m.Monto1), ("Monto 2", m => m.Monto2),
                ("Observaciones", m => m.Observaciones), ("Último cambio", m => m.UltimaModificacion)),
            TipoSector.Reclamos => Escribir(ruta, _registros.Reclamos(idSector, filtro),
                ("N° de caso", r => r.NroCaso), ("Fecha", r => r.Fecha), ("Ubicación", r => r.Ubicacion), ("Vehículo", r => r.Vehiculo),
                ("Verificado", r => r.Verificado), ("Fecha verificación", r => r.FechaVerificacion), ("Verificado por", r => r.VerificadoPor),
                ("Observaciones", r => r.Observaciones), ("Último cambio", r => r.UltimaModificacion)),
            TipoSector.Lotes => Escribir(ruta, _registros.Lotes(idSector, filtro),
                ("N° de lote", l => l.NroLote), ("Fecha", l => l.Fecha), ("Tránsito", l => l.CantTransito), ("Competencia", l => l.CantCompetencia),
                ("Prescriptas", l => l.CantPrescriptas), ("Total", l => l.Total), ("Observaciones", l => l.Observaciones),
                ("Último cambio", l => l.UltimaModificacion)),
            _ => throw new ExcepcionNegocio("Este sector no tiene una planilla para exportar.")
        };
        Registrar("Exportacion", idSector, idSector, "Exportación", $"{filas} filas · {Path.GetFileName(ruta)}");
        return filas;
    }

    private static int Escribir<T>(string ruta, List<T> filas, params (string Titulo, Func<T, object> Valor)[] columnas)
    {
        var sb = new StringBuilder();
        sb.AppendLine(string.Join(";", columnas.Select(c => Celda(c.Titulo))));
        foreach (var f in filas)
            sb.AppendLine(string.Join(";", columnas.Select(c => Celda(Valor(c.Valor(f))))));
        try { File.WriteAllText(ruta, sb.ToString(), new UTF8Encoding(true)); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new ExcepcionNegocio($"No se pudo guardar el archivo: {ex.Message}", ex);
        }
        return filas.Count;
    }

    private static string Valor(object v) => v switch
    {
        DateTime f when f.TimeOfDay == TimeSpan.Zero => f.ToString("dd/MM/yyyy"),
        DateTime f => f.ToString("dd/MM/yyyy HH:mm"),
        _ => Cambios.Formato(v)
    };

    private static string Celda(string s)
    {
        s ??= "";
        // Evita que la planilla interprete el texto como fórmula
        if (s.Length > 0 && "=+-@".Contains(s[0]) && !decimal.TryParse(s, System.Globalization.NumberStyles.Number, Formatos.Cultura, out _))
            s = "'" + s;
        return s.IndexOfAny([';', '"', '\n', '\r']) >= 0 ? "\"" + s.Replace("\"", "\"\"") + "\"" : s;
    }
}
