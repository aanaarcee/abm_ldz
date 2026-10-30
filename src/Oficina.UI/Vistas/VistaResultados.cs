using Oficina.Entidades;
using Oficina.UI.Formularios;

namespace Oficina.UI.Vistas;

/// <summary>Resultados de la búsqueda general, agrupados por sector como en WhatsApp.</summary>
internal class VistaResultados : UserControl
{
    private readonly INavegador _nav;
    private readonly Label _resumen;
    private readonly ListView _lista;

    public VistaResultados(INavegador nav)
    {
        _nav = nav;
        BackColor = Estilo.Fondo;
        _resumen = Estilo.Etiqueta("", Estilo.Subtitulo);
        var ayuda = Estilo.Etiqueta("Doble clic para abrir. Esc borra la búsqueda.", color: Estilo.TextoSuave);

        _lista = Estilo.Lista(("Registro", 280), ("Detalle", 0), ("Fecha", 100));
        _lista.ShowGroups = true;
        _lista.ItemActivate += (_, _) => Abrir();
        _lista.GridLines = false;   // las filas ya se separan por el color de cada sector
        var marco = Estilo.Marco(_lista);

        var tabla = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1 };
        tabla.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        tabla.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        tabla.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        tabla.Controls.Add(_resumen);
        tabla.Controls.Add(ayuda);
        tabla.Controls.Add(marco);
        Controls.Add(tabla);
    }

    public void Mostrar(string texto, List<ResultadoBusqueda> resultados)
    {
        _lista.BeginUpdate();
        _lista.Items.Clear();
        _lista.Groups.Clear();
        foreach (var g in resultados.GroupBy(r => (r.IdSector, r.Sector)).OrderBy(g => g.Key.IdSector))
        {
            var grupo = new ListViewGroup($"{g.Key.Sector}  ({g.Count()})") { HeaderAlignment = HorizontalAlignment.Left };
            _lista.Groups.Add(grupo);
            var fondo = Estilo.FondoSector(g.Key.IdSector);
            foreach (var r in g)
            {
                var item = new ListViewItem([r.Titulo, r.Detalle, Estilo.Fecha(r.Fecha)], grupo) { Tag = r, UseItemStyleForSubItems = false };
                foreach (ListViewItem.ListViewSubItem sub in item.SubItems) sub.BackColor = fondo;
                item.SubItems[0].ForeColor = Estilo.ColorSector(g.Key.IdSector);
                item.SubItems[0].Font = Estilo.Negrita;
                _lista.Items.Add(item);
            }
        }
        _lista.EndUpdate();

        int sectores = _lista.Groups.Count;
        _resumen.Text = resultados.Count == 0
            ? $"No hay resultados para «{texto}»"
            : $"{resultados.Count} resultado{(resultados.Count == 1 ? "" : "s")} para «{texto}» en {sectores} sector{(sectores == 1 ? "" : "es")}"
              + (resultados.Count >= 300 ? " (se muestran los 300 más recientes)" : "");
    }

    private void Abrir()
    {
        if (_lista.SelectedItems.Count == 0 || _lista.SelectedItems[0].Tag is not ResultadoBusqueda r) return;
        if (r.IdActa.HasValue) _nav.AbrirActa(r.IdActa.Value);
        else
        {
            // "Caso RVP-2026-001" o "Lote L-2026-01": se abre el sector filtrado por ese número
            var numero = r.Titulo[(r.Titulo.IndexOf(' ') + 1)..];
            _nav.AbrirSector(r.IdSector, numero);
        }
    }
}
