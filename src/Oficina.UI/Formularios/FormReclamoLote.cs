using Oficina.Entidades;
using Oficina.Negocio;

namespace Oficina.UI.Formularios;

/// <summary>Alta o modificación de un aviso de auto abandonado.</summary>
internal class FormReclamo : FormularioDatos
{
    private readonly ServicioRegistros _servicio;
    private readonly Sector _sector;
    private readonly Reclamo _existente;
    private readonly TextBox _caso, _ubicacion, _vehiculo, _verificadoPor, _obs;
    private readonly DateTimePicker _fecha, _fechaVerif;
    private readonly CheckBox _verificado;

    public FormReclamo(ServicioRegistros servicio, Sector sector, Reclamo existente)
        : base(existente == null ? "Nuevo reclamo" : $"Modificar reclamo {existente.NroCaso}")
    {
        _servicio = servicio;
        _sector = sector;
        _existente = existente;

        Seccion("Aviso de auto abandonado");
        _caso = Campo("N° de caso", Texto(20));
        _fecha = Campo("Fecha del aviso", Fecha());
        _ubicacion = Campo("Ubicación", Texto(150));
        _vehiculo = Campo("Vehículo", Texto(100));
        Nota("Marca, color y patente si se sabe.");
        Seccion("Verificación");
        _verificado = Campo("", new CheckBox { Text = "Alguien fue a ver el auto", AutoSize = true });
        _fechaVerif = Campo("Fecha de verificación", Fecha());
        _verificadoPor = Campo("Verificado por", Texto(80));
        Seccion("Otros datos");
        _obs = Campo("Observaciones", Texto(500, varias: true));

        _verificado.CheckedChanged += (_, _) => _fechaVerif.Enabled = _verificadoPor.Enabled = _verificado.Checked;
        if (existente == null)
            _caso.Text = $"RVP-{DateTime.Today.Year}-";
        else
        {
            _caso.Text = existente.NroCaso;
            _fecha.Value = existente.Fecha;
            _ubicacion.Text = existente.Ubicacion;
            _vehiculo.Text = existente.Vehiculo;
            _verificado.Checked = existente.Verificado;
            if (existente.FechaVerificacion.HasValue) _fechaVerif.Value = existente.FechaVerificacion.Value;
            _verificadoPor.Text = existente.VerificadoPor;
            _obs.Text = existente.Observaciones;
        }
        _fechaVerif.Enabled = _verificadoPor.Enabled = _verificado.Checked;
    }

    protected override bool Guardar()
    {
        var r = new Reclamo
        {
            IdReclamo = _existente?.IdReclamo ?? 0, NroCaso = _caso.Text, Fecha = _fecha.Value.Date, Ubicacion = _ubicacion.Text,
            Vehiculo = _vehiculo.Text, Verificado = _verificado.Checked, FechaVerificacion = _verificado.Checked ? _fechaVerif.Value.Date : null,
            VerificadoPor = _verificadoPor.Text, Observaciones = _obs.Text
        };
        if (_existente == null) _servicio.CargarReclamo(_sector.IdSector, r);
        else _servicio.ModificarReclamo(_sector.IdSector, r);
        return true;
    }
}

/// <summary>Alta o modificación de un lote.</summary>
internal class FormLote : FormularioDatos
{
    private readonly ServicioRegistros _servicio;
    private readonly Sector _sector;
    private readonly Lote _existente;
    private readonly TextBox _nro, _obs;
    private readonly DateTimePicker _fecha;
    private readonly NumericUpDown _transito, _competencia, _prescriptas;
    private readonly Label _total;

    public FormLote(ServicioRegistros servicio, Sector sector, Lote existente)
        : base(existente == null ? "Nuevo lote" : $"Modificar lote {existente.NroLote}")
    {
        _servicio = servicio;
        _sector = sector;
        _existente = existente;

        Seccion("Lote");
        _nro = Campo("N° de lote", Texto(20));
        _fecha = Campo("Fecha", Fecha());
        Seccion("Cantidad de actas");
        _transito = Campo("Tránsito", new NumericUpDown { Maximum = 100_000 });
        _competencia = Campo("Competencia", new NumericUpDown { Maximum = 100_000 });
        _prescriptas = Campo("Prescriptas", new NumericUpDown { Maximum = 100_000 });
        _total = Campo("Total", Estilo.Etiqueta("0", Estilo.Negrita));
        Seccion("Otros datos");
        _obs = Campo("Observaciones", Texto(500, varias: true));

        foreach (var n in new[] { _transito, _competencia, _prescriptas })
            n.ValueChanged += (_, _) => _total.Text = (_transito.Value + _competencia.Value + _prescriptas.Value).ToString("N0", Formatos.Cultura);

        if (existente == null) _nro.Text = $"L-{DateTime.Today.Year}-";
        else
        {
            _nro.Text = existente.NroLote;
            _fecha.Value = existente.Fecha;
            _transito.Value = existente.CantTransito;
            _competencia.Value = existente.CantCompetencia;
            _prescriptas.Value = existente.CantPrescriptas;
            _obs.Text = existente.Observaciones;
        }
    }

    protected override bool Guardar()
    {
        var l = new Lote
        {
            IdLote = _existente?.IdLote ?? 0, NroLote = _nro.Text, Fecha = _fecha.Value.Date, CantTransito = (int)_transito.Value,
            CantCompetencia = (int)_competencia.Value, CantPrescriptas = (int)_prescriptas.Value, Observaciones = _obs.Text
        };
        if (_existente == null) _servicio.CargarLote(_sector.IdSector, l);
        else _servicio.ModificarLote(_sector.IdSector, l);
        return true;
    }
}
