using Oficina.Entidades;
using Oficina.Negocio;

namespace Oficina.UI.Formularios;

/// <summary>
/// Alta o modificación de un movimiento de acta en un sector.
/// En el alta, si el acta (causa + repartición + año) ya existe se completa sola; si no, se cargan sus datos.
/// </summary>
internal class FormMovimiento : FormularioDatos
{
    private readonly ServicioRegistros _servicio;
    private readonly Sector _sector;
    private readonly Movimiento _existente;

    private TextBox _causa, _nroActa, _apellido, _nombre, _documento, _monto1, _monto2, _obsActa;
    private ComboBox _reparticion, _juzgado, _tipoDoc;
    private NumericUpDown _anio;
    private DateTimePicker _fechaActa;
    private Label _estadoActa;
    private Acta _actaEncontrada;

    private readonly DateTimePicker _fecha;
    private readonly ComboBox _detalle;
    private readonly TextBox _remito, _observaciones;

    public FormMovimiento(ServicioRegistros servicio, Sector sector, Movimiento existente)
        : base(existente == null ? $"Nuevo movimiento · {sector.Nombre}" : $"Modificar movimiento · {sector.Nombre}")
    {
        _servicio = servicio;
        _sector = sector;
        _existente = existente;

        if (existente == null) ArmarActa();
        else
        {
            Seccion("Acta");
            Nota($"Causa {existente.NroCausa} · {existente.Reparticion} · {existente.Anio} · Acta {existente.NroActa}"
                 + (existente.Persona != null ? $" · {existente.Persona}" : ""));
        }

        Seccion($"Movimiento en {sector.Nombre}");
        _fecha = Campo("Fecha", Fecha());
        _detalle = Campo("Movimiento", new ComboBox { DropDownStyle = ComboBoxStyle.DropDown, MaxLength = 100 });
        _detalle.Items.AddRange(servicio.SugerenciasDeMovimiento(sector.IdSector).ToArray<object>());
        _detalle.AutoCompleteMode = AutoCompleteMode.SuggestAppend;
        _detalle.AutoCompleteSource = AutoCompleteSource.ListItems;
        _remito = Campo("Remito", Texto(30));
        _observaciones = Campo("Observaciones", Texto(500, varias: true));
        Nota("El movimiento es texto libre hasta que la oficina defina la lista.");

        if (existente != null)
        {
            _fecha.Value = existente.Fecha;
            _detalle.Text = existente.Detalle;
            _remito.Text = existente.Remito;
            _observaciones.Text = existente.Observaciones;
        }
    }

    private void ArmarActa()
    {
        Seccion("Acta");
        Nota("El acta se identifica por N° de causa + repartición + año (el N° de acta se puede repetir).");
        _causa = Campo("N° de causa", Texto(20));
        _reparticion = Campo("Repartición", Lista(_servicio.Reparticiones()));
        _anio = Campo("Año", new NumericUpDown { Minimum = 1990, Maximum = DateTime.Today.Year + 1, Value = DateTime.Today.Year });
        _estadoActa = Estilo.Etiqueta("", Estilo.Negrita, Estilo.Acento);
        _estadoActa.MaximumSize = new Size(Estilo.Px(560), 0);
        Campo("", _estadoActa);

        _nroActa = Campo("N° de acta", Texto(20));
        _fechaActa = Campo("Fecha del acta", Fecha());
        _juzgado = Campo("Juzgado", Lista(_servicio.Juzgados(), conVacio: true));
        _apellido = Campo("Apellido o razón social", Texto(80));
        _nombre = Campo("Nombre", Texto(80));
        _tipoDoc = Campo("Tipo de documento", Lista(new[] { "DNI", "CUIT" }));
        _documento = Campo("N° de documento", Texto(13));
        _monto1 = Campo("Monto 1", Texto(16));
        _monto2 = Campo("Monto 2", Texto(16));
        _obsActa = Campo("Observaciones del acta", Texto(500, varias: true));

        _causa.Leave += (_, _) => BuscarActa();
        _reparticion.SelectedIndexChanged += (_, _) => BuscarActa();
        _anio.ValueChanged += (_, _) => BuscarActa();
        BuscarActa();
    }

    /// <summary>Si el acta ya existe, muestra sus datos y no deja cambiarlos desde acá.</summary>
    private void BuscarActa()
    {
        _actaEncontrada = _servicio.BuscarActa(_causa.Text, (_reparticion.SelectedItem as Reparticion)?.IdReparticion ?? 0, (int)_anio.Value);
        var a = _actaEncontrada;
        bool editable = a == null;
        foreach (Control c in new Control[] { _nroActa, _fechaActa, _juzgado, _apellido, _nombre, _tipoDoc, _documento, _monto1, _monto2, _obsActa })
            c.Enabled = editable;

        if (string.IsNullOrWhiteSpace(_causa.Text)) _estadoActa.Text = "Escriba el N° de causa para ver si el acta ya está cargada.";
        else if (a == null) _estadoActa.Text = "Acta nueva: complete sus datos. Se guarda como acta original.";
        else
        {
            _estadoActa.Text = a.Eliminado
                ? "Esa acta está dada de baja."
                : $"El acta ya existe (versión {a.Version}): se le agrega el movimiento. Para cambiar sus datos use «Modificar acta» en la ficha.";
            _nroActa.Text = a.NroActa;
            _fechaActa.Value = a.FechaActa;
            Elegir<Juzgado>(_juzgado, j => j.IdJuzgado == a.IdJuzgado);
            var persona = a.Persona ?? "";
            int coma = persona.IndexOf(", ", StringComparison.Ordinal);
            _apellido.Text = coma < 0 ? persona : persona[..coma];
            _nombre.Text = coma < 0 ? "" : persona[(coma + 2)..];
            _documento.Text = a.Documento;
            _monto1.Text = Monto(a.Monto1);
            _monto2.Text = Monto(a.Monto2);
            _obsActa.Text = a.Observaciones;
        }
    }

    protected override bool Guardar()
    {
        var mov = new Movimiento
        {
            IdMovimiento = _existente?.IdMovimiento ?? 0, Fecha = _fecha.Value.Date, Detalle = _detalle.Text,
            Remito = _remito.Text, Observaciones = _observaciones.Text
        };
        if (_existente != null)
        {
            _servicio.ModificarMovimiento(mov);
            return true;
        }

        var acta = new Acta
        {
            NroCausa = _causa.Text, IdReparticion = (_reparticion.SelectedItem as Reparticion)?.IdReparticion ?? 0, Anio = (int)_anio.Value,
            NroActa = _nroActa.Text, FechaActa = _fechaActa.Value.Date, IdJuzgado = (_juzgado.SelectedItem as Juzgado)?.IdJuzgado,
            Monto1 = LeerMonto(_monto1, "Monto 1"), Monto2 = LeerMonto(_monto2, "Monto 2"), Observaciones = _obsActa.Text
        };
        var persona = new Persona { Apellido = _apellido.Text, Nombre = _nombre.Text, TipoDocumento = (string)_tipoDoc.SelectedItem, NroDocumento = _documento.Text };
        _servicio.CargarMovimiento(_sector.IdSector, acta, persona, mov);
        return true;
    }
}
