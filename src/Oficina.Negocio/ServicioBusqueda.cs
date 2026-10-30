using Oficina.Datos;
using Oficina.Entidades;

namespace Oficina.Negocio;

// Una tarjeta de la pantalla de inicio
public class TarjetaSector
{
    public Sector Sector { get; set; }
    public int Cantidad { get; set; }
    public string Detalle { get; set; }
}

// Búsqueda general (en todos los sectores que la cuenta puede ver) y datos de la pantalla de inicio.
public class ServicioBusqueda : ServicioBase
{
    public const int MinimoCaracteres = 2;

    public ServicioBusqueda(Sesion sesion, IDatosRegistros datos) : base(sesion, datos) { }

    // Busca en todo lo que la cuenta puede leer. Con idSector, solo dentro de ese sector 
    public List<ResultadoBusqueda> Buscar(string texto, int? idSector = null)
    {
        texto = texto?.Trim() ?? "";
        if (texto.Length < MinimoCaracteres) return new();
        if (idSector.HasValue) Sesion.Exigir(idSector.Value, Accion.Leer);
        var ids = Sesion.IdsVisibles.ToList();
        return ids.Count == 0 ? new() : Datos.Buscar(texto, ids, idSector);
    }

    public List<TarjetaSector> Tarjetas()
    {
        var porSector = Datos.ContarMovimientosPorSector();
        var tarjetas = new List<TarjetaSector>();
        List<Reclamo> reclamos = null;
        List<Lote> lotes = null;
        foreach (var s in Sesion.SectoresVisibles)
        {
            var t = new TarjetaSector { Sector = s };
            switch (s.Tipo)
            {
                case TipoSector.Actas:
                    t.Cantidad = porSector.GetValueOrDefault(s.IdSector);
                    t.Detalle = t.Cantidad == 1 ? "movimiento" : "movimientos";
                    break;
                case TipoSector.Reclamos:
                    reclamos ??= Datos.ListarReclamos();
                    t.Cantidad = reclamos.Count;
                    t.Detalle = $"reclamos · {reclamos.Count(r => !r.Verificado)} sin verificar";
                    break;
                case TipoSector.Lotes:
                    lotes ??= Datos.ListarLotes();
                    t.Cantidad = lotes.Count;
                    t.Detalle = $"lotes · {lotes.Sum(l => l.Total)} actas";
                    break;
                default:
                    // Solo suma los sectores que la cuenta ve
                    t.Cantidad = Sesion.IdsVisibles.Sum(id => porSector.GetValueOrDefault(id));
                    t.Detalle = "movimientos en total";
                    break;
            }
            tarjetas.Add(t);
        }
        return tarjetas;
    }

    public List<EntradaHistorial> UltimosCambios(int cantidad = 15) => Sesion.Filtrar(Datos.ListarUltimosCambios(Sesion.IdsVisibles, cantidad));

    // resumen para el sector estadisticas
    public List<(string Sector, int Cantidad, string Detalle)> Resumen()
        => Tarjetas().Where(t => t.Sector.Tipo != TipoSector.Consulta).Select(t => (t.Sector.Nombre, t.Cantidad, t.Detalle)).ToList();
}
