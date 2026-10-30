using Oficina.Entidades;
using Oficina.UI.Formularios;

namespace Oficina.UI.Vistas;

/// <summary>Pantalla de inicio: una tarjeta por sector y los últimos cambios.</summary>
internal class VistaInicio : UserControl, IRefrescable
{
    private readonly INavegador _nav;
    private readonly FlowLayoutPanel _tarjetas;
    private readonly ListView _cambios;

    public VistaInicio(INavegador nav)
    {
        _nav = nav;
        BackColor = Estilo.Fondo;

        var titulo = Estilo.Etiqueta($"Hola, {nav.Sesion.Rol.Nombre}", Estilo.Titulo);
        var ayuda = Estilo.Etiqueta("Escriba arriba para buscar en todos los sectores, o elija un sector para ver su planilla.", color: Estilo.TextoSuave);
        _tarjetas = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Margin = new Padding(0, Estilo.Px(10), 0, Estilo.Px(10)) };

        _cambios = Estilo.Lista(("Fecha", 130), ("Sector", 160), ("Cuenta", 110), ("Acción", 110), ("Detalle", 0));
        var marco = Estilo.Marco(_cambios);

        var tabla = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1 };
        tabla.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        tabla.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        tabla.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        tabla.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        tabla.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        tabla.Controls.Add(titulo);
        tabla.Controls.Add(ayuda);
        tabla.Controls.Add(_tarjetas);
        tabla.Controls.Add(Estilo.Etiqueta("Últimos cambios", Estilo.Subtitulo));
        tabla.Controls.Add(marco);
        Controls.Add(tabla);

        Refrescar();
    }

    public void Refrescar()
    {
        var busqueda = _nav.App.Busqueda(_nav.Sesion);
        _tarjetas.SuspendLayout();
        foreach (Control c in _tarjetas.Controls.Cast<Control>().ToList()) c.Dispose();
        foreach (var t in busqueda.Tarjetas())
            _tarjetas.Controls.Add(Tarjeta(t.Sector, t.Cantidad, t.Detalle));
        _tarjetas.ResumeLayout();

        _cambios.BeginUpdate();
        _cambios.Items.Clear();
        foreach (var h in busqueda.UltimosCambios(20))
        {
            var item = new ListViewItem([Estilo.FechaHora(h.Fecha), h.Sector, h.Usuario, h.Accion, h.Detalle]) { UseItemStyleForSubItems = false };
            if (h.IdSector.HasValue)
            {
                item.SubItems[1].ForeColor = Estilo.ColorSector(h.IdSector.Value);
                item.SubItems[1].Font = Estilo.Negrita;
            }
            item.SubItems[3].ForeColor = Estilo.ColorAccion(h.Accion);
            _cambios.Items.Add(item);
        }
        _cambios.EndUpdate();
    }

    private Control Tarjeta(Sector sector, int cantidad, string detalle)
    {
        var p = new TableLayoutPanel
        {
            AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, MinimumSize = new Size(Estilo.Px(220), 0),
            BackColor = Estilo.Panel, Cursor = Cursors.Hand, ColumnCount = 1,
            Margin = new Padding(0, 0, Estilo.Px(12), Estilo.Px(12)), Padding = new Padding(Estilo.Px(18), Estilo.Px(8), Estilo.Px(8), Estilo.Px(6))
        };
        var color = Estilo.ColorSector(sector.IdSector);
        p.Paint += (_, e) => ControlPaint.DrawBorder(e.Graphics, p.ClientRectangle, Estilo.Borde, ButtonBorderStyle.Solid);
        Estilo.FranjaIzquierda(p, color, 6);
        p.Controls.Add(Estilo.Etiqueta(sector.Nombre, Estilo.Negrita, color));
        p.Controls.Add(Estilo.Etiqueta(cantidad.ToString("N0", Negocio.Formatos.Cultura), Estilo.Numero, Estilo.Texto));
        p.Controls.Add(Estilo.Etiqueta(detalle, color: Estilo.TextoSuave));
        void Abrir(object s, EventArgs e) => _nav.AbrirSector(sector.IdSector);
        p.Click += Abrir;
        foreach (Control c in p.Controls) { c.Click += Abrir; c.Cursor = Cursors.Hand; }
        return p;
    }
}
