using Oficina.Negocio;

namespace Oficina.UI.Formularios;

/// <summary>Ficha del acta: sus datos, el recorrido por los sectores, la original contra la actual, las versiones y el historial.</summary>
internal class FormActa : Form
{
    private readonly INavegador _nav;
    private readonly int _idActa;
    private readonly ServicioRegistros _servicio;
    private readonly Label _titulo, _version;
    private readonly TableLayoutPanel _datos;
    private readonly ListView _recorrido, _comparacion, _versiones, _historial;
    private readonly Button _modificar;
    private FichaActa _ficha;

    public bool HuboCambios { get; private set; }

    public FormActa(INavegador nav, int idActa)
    {
        _nav = nav;
        _idActa = idActa;
        _servicio = nav.App.Registros(nav.Sesion);
        Text = "Ficha del acta";
        Font = Estilo.Letra;
        BackColor = Estilo.Fondo;
        StartPosition = FormStartPosition.CenterParent;
        Size = new Size(Estilo.Px(1080), Estilo.Px(720));
        MinimizeBox = false;
        ShowInTaskbar = false;
        Padding = new Padding(Estilo.Px(18));

        _titulo = Estilo.Etiqueta("", Estilo.Titulo);
        _version = Estilo.Etiqueta("", color: Estilo.TextoSuave);
        _modificar = Estilo.Boton("Modificar acta", (_, _) => Modificar(), principal: true);
        var cerrar = Estilo.Boton("Cerrar", (_, _) => Close());

        var arriba = new TableLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, ColumnCount = 3 };
        arriba.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        arriba.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        arriba.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        arriba.Controls.Add(_titulo, 0, 0);
        arriba.Controls.Add(_modificar, 1, 0);
        arriba.Controls.Add(cerrar, 2, 0);
        arriba.Controls.Add(_version, 0, 1);

        _datos = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, AutoSize = true, ColumnCount = 4, BackColor = Estilo.Panel,
            Padding = new Padding(Estilo.Px(12), Estilo.Px(8), Estilo.Px(12), Estilo.Px(8)), Margin = new Padding(0, Estilo.Px(8), 0, Estilo.Px(10))
        };
        _datos.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        _datos.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        _datos.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        _datos.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        _datos.Paint += (_, e) => ControlPaint.DrawBorder(e.Graphics, _datos.ClientRectangle, Estilo.Borde, ButtonBorderStyle.Solid);

        _recorrido = Estilo.Lista(("Fecha", 95), ("Sector", 150), ("Movimiento", 230), ("Remito", 85), ("Observaciones", 0));
        _comparacion = Estilo.Lista(("Dato", 150), ("Acta original", 400), ("Acta actual", 0));
        _versiones = Estilo.Lista(("Versión", 90), ("Fecha", 130), ("Motivo", 0), ("N° acta", 80), ("Monto 1", 100), ("Monto 2", 100), ("Persona", 180));
        _historial = Estilo.Lista(("Fecha", 130), ("Sector", 150), ("Cuenta", 100), ("Acción", 100), ("Detalle", 0));

        var pestañas = new TabControl { Dock = DockStyle.Fill, Padding = new Point(Estilo.Px(12), Estilo.Px(5)) };
        foreach (var (titulo, lista) in new[] { ("Recorrido por los sectores", _recorrido), ("Original y actual", _comparacion), ("Versiones", _versiones), ("Historial", _historial) })
        {
            var p = new TabPage(titulo) { BackColor = Estilo.Panel };
            p.Controls.Add(Estilo.Marco(lista));
            pestañas.TabPages.Add(p);
        }

        var tabla = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1 };
        tabla.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        tabla.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        tabla.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        tabla.Controls.Add(arriba);
        tabla.Controls.Add(_datos);
        tabla.Controls.Add(pestañas);
        Controls.Add(tabla);
        CancelButton = cerrar;

        Cargar();
    }

    private void Cargar()
    {
        _ficha = _servicio.ObtenerFicha(_idActa);
        var a = _ficha.Acta;
        Text = $"Ficha del acta · {a.Clave}";
        _titulo.Text = a.Clave;
        _version.Text = a.Version == 1
            ? $"Acta original, sin modificaciones · cargada el {Estilo.FechaHora(a.FechaAlta)}"
            : $"Versión {a.Version} · modificada el {Estilo.FechaHora(a.FechaModif ?? a.FechaAlta)} · ver «Original y actual»";
        _modificar.Visible = _ficha.PuedeModificar && !a.Eliminado;

        _datos.SuspendLayout();
        _datos.Controls.Clear();
        void Dato(string etiqueta, string valor)
        {
            _datos.Controls.Add(Estilo.Etiqueta(etiqueta, color: Estilo.TextoSuave));
            var v = Estilo.Etiqueta(string.IsNullOrEmpty(valor) ? "—" : valor, Estilo.Negrita);
            v.MaximumSize = new Size(Estilo.Px(380), 0);
            _datos.Controls.Add(v);
        }
        Dato("N° de acta", a.NroActa);
        Dato("Fecha del acta", Estilo.Fecha(a.FechaActa));
        Dato("Persona", a.Persona);
        Dato(_ficha.Persona?.TipoDocumento ?? "Documento", a.Documento);
        Dato("Juzgado", a.Juzgado);
        Dato("Repartición", a.Reparticion);
        Dato("Monto 1", a.Monto1?.ToString("N2", Formatos.Cultura));
        Dato("Monto 2", a.Monto2?.ToString("N2", Formatos.Cultura));
        Dato("Observaciones", a.Observaciones);
        _datos.ResumeLayout();

        Llenar(_recorrido, _ficha.Recorrido.Select(m => ConSector(new ListViewItem([Estilo.Fecha(m.Fecha), m.Sector, m.Detalle, m.Remito, m.Observaciones]), m.IdSector)));
        Llenar(_comparacion, _ficha.Comparacion.Select(c => new ListViewItem([c.Campo, c.Original, c.Actual])
        {
            BackColor = c.Cambio ? Estilo.Resaltado : Estilo.Panel, Font = c.Cambio ? Estilo.Negrita : Estilo.Letra
        }));
        Llenar(_versiones, _ficha.Versiones.Select(v => new ListViewItem(
        [
            v.NroVersion == 1 ? "1 (original)" : v.NroVersion.ToString(), Estilo.FechaHora(v.Fecha), v.Motivo, v.NroActa,
            v.Monto1?.ToString("N2", Formatos.Cultura), v.Monto2?.ToString("N2", Formatos.Cultura), v.Persona
        ])));
        Llenar(_historial, _ficha.Historial.Select(h =>
        {
            var item = ConSector(new ListViewItem([Estilo.FechaHora(h.Fecha), h.Sector, h.Usuario, h.Accion, h.Detalle]), h.IdSector);
            item.SubItems[3].ForeColor = Estilo.ColorAccion(h.Accion);
            return item;
        }));
    }

    /// <summary>La columna Sector (la segunda) como etiqueta: fondo suave y texto del color del sector.</summary>
    private static ListViewItem ConSector(ListViewItem item, int? idSector)
    {
        item.UseItemStyleForSubItems = false;
        if (idSector is not int id) return item;
        item.SubItems[1].BackColor = Estilo.FondoSector(id);
        item.SubItems[1].ForeColor = Estilo.ColorSector(id);
        item.SubItems[1].Font = Estilo.Negrita;
        return item;
    }

    private static void Llenar(ListView lista, IEnumerable<ListViewItem> items)
    {
        lista.BeginUpdate();
        lista.Items.Clear();
        lista.Items.AddRange(items.ToArray());
        lista.EndUpdate();
    }

    private void Modificar()
    {
        using var f = new FormEditarActa(_servicio, _ficha.Acta, _ficha.Persona);
        if (f.ShowDialog(this) != DialogResult.OK) return;
        HuboCambios = true;
        Cargar();
    }
}
