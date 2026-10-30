using Oficina.Entidades;
using Oficina.Negocio;

namespace Oficina.UI.Formularios;

/// <summary>Modifica los datos del acta. Cada cambio guarda una versión nueva; la original queda intacta.</summary>
internal class FormEditarActa : FormularioDatos
{
    private readonly ServicioRegistros _servicio;
    private readonly Acta _acta;
    private readonly TextBox _nroActa, _apellido, _nombre, _documento, _monto1, _monto2, _obs, _motivo;
    private readonly DateTimePicker _fechaActa;
    private readonly ComboBox _juzgado, _tipoDoc;

    public FormEditarActa(ServicioRegistros servicio, Acta acta, Persona persona) : base($"Modificar acta · Causa {acta.NroCausa}")
    {
        _servicio = servicio;
        _acta = acta;

        Seccion(acta.Clave);
        Nota($"Hoy está en la versión {acta.Version}. Al guardar se crea la versión {acta.Version + 1}; la original no se pierde. " +
             "La causa, la repartición y el año no se cambian porque identifican el acta.");
        _nroActa = Campo("N° de acta", Texto(20));
        _fechaActa = Campo("Fecha del acta", Fecha());
        _juzgado = Campo("Juzgado", Lista(servicio.Juzgados(), conVacio: true));
        _apellido = Campo("Apellido o razón social", Texto(80));
        _nombre = Campo("Nombre", Texto(80));
        _tipoDoc = Campo("Tipo de documento", Lista(new[] { "DNI", "CUIT" }));
        _documento = Campo("N° de documento", Texto(13));
        _monto1 = Campo("Monto 1", Texto(16));
        _monto2 = Campo("Monto 2", Texto(16));
        _obs = Campo("Observaciones", Texto(500, varias: true));
        Seccion("Motivo del cambio");
        _motivo = Campo("Motivo", Texto(200));

        _nroActa.Text = acta.NroActa;
        _fechaActa.Value = acta.FechaActa;
        Elegir<Juzgado>(_juzgado, j => j.IdJuzgado == acta.IdJuzgado);
        if (persona != null)
        {
            _apellido.Text = persona.Apellido;
            _nombre.Text = persona.Nombre;
            Elegir<string>(_tipoDoc, t => t == persona.TipoDocumento);
            _documento.Text = persona.NroDocumento;
        }
        _monto1.Text = Monto(acta.Monto1);
        _monto2.Text = Monto(acta.Monto2);
        _obs.Text = acta.Observaciones;
    }

    protected override bool Guardar()
    {
        var cambios = new Acta
        {
            IdActa = _acta.IdActa, NroActa = _nroActa.Text, FechaActa = _fechaActa.Value.Date, IdJuzgado = (_juzgado.SelectedItem as Juzgado)?.IdJuzgado,
            Monto1 = LeerMonto(_monto1, "Monto 1"), Monto2 = LeerMonto(_monto2, "Monto 2"), Observaciones = _obs.Text
        };
        var persona = new Persona { Apellido = _apellido.Text, Nombre = _nombre.Text, TipoDocumento = (string)_tipoDoc.SelectedItem, NroDocumento = _documento.Text };
        _servicio.ModificarActa(cambios, persona, _motivo.Text);
        return true;
    }
}
