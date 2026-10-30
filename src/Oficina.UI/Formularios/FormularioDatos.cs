using Oficina.Negocio;

namespace Oficina.UI.Formularios;

/// <summary>
/// Base de los formularios de carga: una columna de etiquetas y otra de campos, con Guardar y Cancelar abajo.
/// Si Guardar() lanza ExcepcionNegocio se muestra el mensaje y el formulario queda abierto.
/// </summary>
internal abstract class FormularioDatos : Form
{
    private readonly TableLayoutPanel _tabla;
    protected Button BotonGuardar { get; }

    protected FormularioDatos(string titulo)
    {
        Text = titulo;
        Font = Estilo.Letra;
        BackColor = Estilo.Panel;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        MinimizeBox = MaximizeBox = false;
        ShowInTaskbar = false;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        Padding = new Padding(Estilo.Px(16));

        _tabla = new TableLayoutPanel { AutoSize = true, ColumnCount = 2, Dock = DockStyle.Top };
        _tabla.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        _tabla.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, Estilo.Px(380)));

        BotonGuardar = Estilo.Boton("Guardar", (_, _) => AlGuardar(), principal: true);
        var cancelar = Estilo.Boton("Cancelar");
        cancelar.DialogResult = DialogResult.Cancel;
        var botones = new FlowLayoutPanel
        {
            AutoSize = true, FlowDirection = FlowDirection.RightToLeft, Dock = DockStyle.Bottom, Padding = new Padding(0, Estilo.Px(12), 0, 0)
        };
        botones.Controls.AddRange([cancelar, BotonGuardar]);

        var contenedor = new TableLayoutPanel { AutoSize = true, ColumnCount = 1, Dock = DockStyle.Fill };
        contenedor.Controls.Add(_tabla);
        contenedor.Controls.Add(botones);
        Controls.Add(contenedor);
        AcceptButton = null;   // Enter no guarda: en los campos de observaciones se escribe con varias líneas
        CancelButton = cancelar;
    }

    /// <summary>Guarda los datos (llamando a la capa de negocio). Devuelve falso si no hay que cerrar.</summary>
    protected abstract bool Guardar();

    private void AlGuardar()
    {
        try
        {
            Cursor.Current = Cursors.WaitCursor;
            if (Guardar()) DialogResult = DialogResult.OK;
        }
        catch (ExcepcionNegocio ex) { Dialogo.MostrarError(ex); }
        finally { Cursor.Current = Cursors.Default; }
    }

    protected void Seccion(string texto)
    {
        var l = Estilo.Etiqueta(texto, Estilo.Subtitulo, Estilo.Acento);
        l.Margin = new Padding(0, _tabla.Controls.Count == 0 ? 0 : Estilo.Px(10), 0, Estilo.Px(4));
        _tabla.Controls.Add(l);
        _tabla.SetColumnSpan(l, 2);
    }

    protected T Campo<T>(string etiqueta, T control) where T : Control
    {
        var l = Estilo.Etiqueta(etiqueta, color: Estilo.TextoSuave);
        l.Anchor = AnchorStyles.Left;
        control.Anchor = AnchorStyles.Left | AnchorStyles.Right;
        control.Margin = new Padding(0, Estilo.Px(3), 0, Estilo.Px(3));
        _tabla.Controls.Add(l);
        _tabla.Controls.Add(control);
        return control;
    }

    protected void Nota(string texto)
    {
        var l = Estilo.Etiqueta(texto, color: Estilo.TextoSuave);
        l.MaximumSize = new Size(Estilo.Px(560), 0);
        _tabla.Controls.Add(l);
        _tabla.SetColumnSpan(l, 2);
    }

    // ---------- Controles de uso común ----------

    protected static TextBox Texto(int largoMaximo, bool varias = false) => new()
    {
        MaxLength = largoMaximo, Multiline = varias, Height = varias ? Estilo.Px(56) : 0, ScrollBars = varias ? ScrollBars.Vertical : ScrollBars.None
    };

    protected static DateTimePicker Fecha(bool opcional = false) => new()
    {
        Format = DateTimePickerFormat.Short, ShowCheckBox = opcional, MaxDate = DateTime.Today.AddDays(1).AddTicks(-1)
    };

    protected static ComboBox Lista<T>(IEnumerable<T> elementos, bool conVacio = false)
    {
        var c = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
        if (conVacio) c.Items.Add("(sin dato)");
        foreach (var e in elementos) c.Items.Add(e);
        if (c.Items.Count > 0) c.SelectedIndex = 0;
        return c;
    }

    protected static void Elegir<T>(ComboBox c, Func<T, bool> condicion)
    {
        for (int i = 0; i < c.Items.Count; i++)
            if (c.Items[i] is T t && condicion(t)) { c.SelectedIndex = i; return; }
        c.SelectedIndex = c.Items.Count > 0 ? 0 : -1;
    }

    protected static decimal? LeerMonto(TextBox caja, string campo)
    {
        var t = caja.Text.Trim().Replace("$", "").Trim();
        if (t == "") return null;
        if (decimal.TryParse(t, System.Globalization.NumberStyles.Number, Formatos.Cultura, out var m)) return Math.Round(m, 2);
        throw new ExcepcionNegocio($"{campo} no es un número válido (use coma para los decimales, por ejemplo 12500,50).");
    }

    protected static string Monto(decimal? m) => m?.ToString("N2", Formatos.Cultura) ?? "";
}
