using Oficina.Entidades;
using Oficina.Negocio;
using Oficina.UI.Formularios;

namespace Oficina.UI.Vistas;

/// <summary>
/// La planilla de un sector: grilla con buscador propio y los botones que el rol puede usar.
/// Según el tipo de sector muestra movimientos de actas, reclamos, lotes o el resumen de estadísticas.
/// </summary>
internal class VistaSector : UserControl, IRefrescable
{
    private readonly INavegador _nav;
    private readonly Sector _sector;
    private readonly ServicioRegistros _servicio;
    private readonly DataGridView _grilla;
    private readonly TextBox _filtro;
    private readonly Label _cantidad;
    private readonly System.Windows.Forms.Timer _espera = new() { Interval = 250 };

    public VistaSector(INavegador nav, Sector sector, string filtroInicial = null)
    {
        _nav = nav;
        _sector = sector;
        _servicio = nav.App.Registros(nav.Sesion);
        BackColor = Estilo.Fondo;
        var sesion = nav.Sesion;
        bool puedeCargar = sesion.Puede(sector.IdSector, Accion.Cargar) && sector.Tipo != TipoSector.Consulta;
        bool puedeModificar = sesion.Puede(sector.IdSector, Accion.Modificar) && sector.Tipo != TipoSector.Consulta;
        bool puedeEliminar = sesion.Puede(sector.IdSector, Accion.Eliminar) && sector.Tipo != TipoSector.Consulta;

        // ---------- Encabezado: título, buscador del sector y botones ----------
        var titulo = Estilo.Etiqueta(sector.Nombre, Estilo.Titulo, Estilo.ColorSector(sector.IdSector));
        _filtro = Estilo.Buscador($"Buscar en {sector.Nombre}…", 340);
        _filtro.Text = filtroInicial ?? "";
        _filtro.TextChanged += (_, _) => { _espera.Stop(); _espera.Start(); };
        _filtro.KeyDown += (_, e) => { if (e.KeyCode == Keys.Escape) { _filtro.Clear(); e.SuppressKeyPress = true; } };
        _espera.Tick += (_, _) => { _espera.Stop(); Refrescar(); };
        _cantidad = Estilo.Etiqueta("", color: Estilo.TextoSuave);
        _cantidad.Anchor = AnchorStyles.Left;

        var botones = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, Margin = new Padding(0, Estilo.Px(8), 0, Estilo.Px(8)), WrapContents = false };
        botones.Controls.Add(_filtro);
        botones.Controls.Add(new Label { Width = Estilo.Px(10) });
        if (puedeCargar) botones.Controls.Add(Estilo.Boton("Nuevo", (_, _) => Nuevo(), principal: true));
        if (puedeModificar) botones.Controls.Add(Estilo.Boton("Modificar", (_, _) => Modificar()));
        if (puedeEliminar) botones.Controls.Add(Estilo.Boton("Eliminar", (_, _) => Eliminar()));
        if (sector.Tipo == TipoSector.Actas) botones.Controls.Add(Estilo.Boton("Ver acta", (_, _) => VerActa()));
        if (sector.Tipo != TipoSector.Consulta) botones.Controls.Add(Estilo.Boton("Exportar CSV", (_, _) => Exportar()));
        botones.Controls.Add(_cantidad);

        // ---------- Grilla ----------
        _grilla = Estilo.Grilla();
        Estilo.ColorearGrilla(_grilla, sector.IdSector);
        ArmarColumnas();
        _grilla.CellDoubleClick += (_, e) =>
        {
            if (e.RowIndex < 0) return;
            if (sector.Tipo == TipoSector.Actas) VerActa();
            else if (puedeModificar) Modificar();
        };
        _grilla.KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; if (sector.Tipo == TipoSector.Actas) VerActa(); else if (puedeModificar) Modificar(); }
            if (e.KeyCode == Keys.Delete && puedeEliminar) { e.SuppressKeyPress = true; Eliminar(); }
        };
        var marco = Estilo.Marco(_grilla);

        // No se describen los permisos: los botones aparecen solo si se pueden usar
        var ayuda = sector.Tipo switch
        {
            TipoSector.Consulta => "Resumen de los sectores.",
            TipoSector.Actas => "Doble clic en una fila para ver la ficha del acta.",
            _ => "Use el buscador para encontrar una fila."
        };
        var tabla = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1 };
        tabla.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        tabla.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        tabla.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        tabla.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        tabla.Controls.Add(titulo);
        tabla.Controls.Add(Estilo.Etiqueta(ayuda, color: Estilo.TextoSuave));
        tabla.Controls.Add(botones);
        tabla.Controls.Add(marco);
        Controls.Add(tabla);

        if (sector.Tipo == TipoSector.Consulta) _filtro.Visible = false;
        Refrescar();
    }

    private void ArmarColumnas()
    {
        var g = _grilla;
        switch (_sector.Tipo)
        {
            case TipoSector.Actas:
                g.Columna(nameof(Movimiento.Fecha), "Fecha", 90, "dd/MM/yyyy");
                g.Columna(nameof(Movimiento.NroCausa), "N° causa", 80);
                g.Columna(nameof(Movimiento.Reparticion), "Repartición", 130);
                g.Columna(nameof(Movimiento.Anio), "Año", 50);
                g.Columna(nameof(Movimiento.NroActa), "N° acta", 70);
                g.Columna(nameof(Movimiento.Persona), "Persona");
                g.Columna(nameof(Movimiento.Documento), "Documento", 95);
                g.Columna(nameof(Movimiento.Detalle), "Movimiento");
                g.Columna(nameof(Movimiento.Remito), "Remito", 75);
                g.Columna(nameof(Movimiento.Observaciones), "Observaciones");
                break;
            case TipoSector.Reclamos:
                g.Columna(nameof(Reclamo.NroCaso), "N° de caso", 110);
                g.Columna(nameof(Reclamo.Fecha), "Fecha", 90, "dd/MM/yyyy");
                g.Columna(nameof(Reclamo.Ubicacion), "Ubicación");
                g.Columna(nameof(Reclamo.Vehiculo), "Vehículo");
                g.Columna(nameof(Reclamo.Verificado), "Estado", 100);
                g.Columna(nameof(Reclamo.FechaVerificacion), "Verificado el", 100, "dd/MM/yyyy");
                g.Columna(nameof(Reclamo.VerificadoPor), "Verificado por", 140);
                g.Columna(nameof(Reclamo.Observaciones), "Observaciones");
                g.CellFormatting += (_, e) =>
                {
                    if (g.Columns[e.ColumnIndex].Name != nameof(Reclamo.Verificado) || e.Value is not bool v) return;
                    e.Value = v ? "Verificado" : "Sin verificar";
                    e.CellStyle.ForeColor = v ? Estilo.Positivo : Estilo.Pendiente;
                    e.CellStyle.Font = Estilo.Negrita;
                    e.FormattingApplied = true;
                };
                break;
            case TipoSector.Lotes:
                g.Columna(nameof(Lote.NroLote), "N° de lote", 110);
                g.Columna(nameof(Lote.Fecha), "Fecha", 90, "dd/MM/yyyy");
                g.Columna(nameof(Lote.CantTransito), "Tránsito", 80, "N0");
                g.Columna(nameof(Lote.CantCompetencia), "Competencia", 95, "N0");
                g.Columna(nameof(Lote.CantPrescriptas), "Prescriptas", 90, "N0");
                g.Columna(nameof(Lote.Total), "Total", 70, "N0");
                g.Columna(nameof(Lote.Observaciones), "Observaciones");
                break;
            default:
                g.Columna("Sector", "Sector", 220);
                g.Columna("Cantidad", "Cantidad", 90, "N0");
                g.Columna("Detalle", "Detalle");
                g.CellFormatting += (_, e) =>
                {
                    if (g.Columns[e.ColumnIndex].Name != "Sector" || e.Value is not string nombre) return;
                    var s = _nav.Sesion.Sectores.FirstOrDefault(x => x.Nombre == nombre);
                    if (s == null) return;
                    e.CellStyle.ForeColor = Estilo.ColorSector(s.IdSector);
                    e.CellStyle.BackColor = Estilo.FondoSector(s.IdSector);
                    e.CellStyle.Font = Estilo.Negrita;
                };
                break;
        }
    }

    public void Refrescar()
    {
        var filtro = _filtro.Text;
        int? seleccionado = IdSeleccionado();
        object datos = _sector.Tipo switch
        {
            TipoSector.Actas => _servicio.Movimientos(_sector.IdSector, filtro),
            TipoSector.Reclamos => _servicio.Reclamos(_sector.IdSector, filtro),
            TipoSector.Lotes => _servicio.Lotes(_sector.IdSector, filtro),
            _ => _nav.App.Busqueda(_nav.Sesion).Resumen().Select(r => new { r.Sector, r.Cantidad, r.Detalle }).ToList()
        };
        _grilla.DataSource = datos;
        int filas = _grilla.Rows.Count;
        _cantidad.Text = $"{filas} fila{(filas == 1 ? "" : "s")}";
        if (seleccionado.HasValue)
            foreach (DataGridViewRow fila in _grilla.Rows)
                if (Id(fila.DataBoundItem) == seleccionado) { _grilla.CurrentCell = fila.Cells[0]; break; }
    }

    private static int? Id(object fila) => fila switch
    {
        Movimiento m => m.IdMovimiento,
        Reclamo r => r.IdReclamo,
        Lote l => l.IdLote,
        _ => null
    };

    private int? IdSeleccionado() => Id(_grilla.CurrentRow?.DataBoundItem);

    private object Seleccionado()
    {
        var item = _grilla.CurrentRow?.DataBoundItem;
        if (item == null) Dialogo.Aviso("Primero elija una fila de la grilla.");
        return item;
    }

    // ---------- Acciones ----------

    private void Nuevo()
    {
        using Form f = _sector.Tipo switch
        {
            TipoSector.Actas => new FormMovimiento(_servicio, _sector, null),
            TipoSector.Reclamos => new FormReclamo(_servicio, _sector, null),
            _ => new FormLote(_servicio, _sector, null)
        };
        if (f.ShowDialog(this) == DialogResult.OK) Refrescar();
    }

    private void Modificar()
    {
        var item = Seleccionado();
        if (item == null) return;
        using Form f = item switch
        {
            Movimiento m => new FormMovimiento(_servicio, _sector, m),
            Reclamo r => new FormReclamo(_servicio, _sector, _servicio.ObtenerReclamo(_sector.IdSector, r.IdReclamo)),
            Lote l => new FormLote(_servicio, _sector, _servicio.ObtenerLote(_sector.IdSector, l.IdLote)),
            _ => null
        };
        if (f != null && f.ShowDialog(this) == DialogResult.OK) Refrescar();
    }

    private void Eliminar()
    {
        var item = Seleccionado();
        if (item == null) return;
        string que = item switch
        {
            Movimiento m => $"el movimiento «{m.Detalle}» de la causa {m.NroCausa}",
            Reclamo r => $"el reclamo {r.NroCaso}",
            Lote l => $"el lote {l.NroLote}",
            _ => "la fila"
        };
        var motivo = Dialogo.PedirTexto(this, "Eliminar", $"Se va a dar de baja {que}.\nQueda guardado en el historial; no se borra.\n\nMotivo (opcional):");
        if (motivo == null) return;
        switch (item)
        {
            case Movimiento m: _servicio.EliminarMovimiento(m.IdMovimiento, motivo); break;
            case Reclamo r: _servicio.EliminarReclamo(_sector.IdSector, r.IdReclamo, motivo); break;
            case Lote l: _servicio.EliminarLote(_sector.IdSector, l.IdLote, motivo); break;
        }
        Refrescar();
    }

    private void VerActa()
    {
        if (Seleccionado() is Movimiento m) _nav.AbrirActa(m.IdActa);
    }

    private void Exportar()
    {
        using var d = new SaveFileDialog
        {
            Title = "Exportar para respaldo en Drive", Filter = "Planilla CSV (*.csv)|*.csv",
            InitialDirectory = ExportadorCsv.CarpetaSugerida(), FileName = ExportadorCsv.NombreSugerido(_sector)
        };
        if (d.ShowDialog(this) != DialogResult.OK) return;
        int filas = _nav.App.Exportador(_nav.Sesion).ExportarSector(_sector.IdSector, _filtro.Text, d.FileName);
        Dialogo.Aviso($"Se exportaron {filas} filas a:\n{d.FileName}\n\nSi la carpeta es de Google Drive, se sube sola como respaldo.");
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) _espera.Dispose();
        base.Dispose(disposing);
    }
}
