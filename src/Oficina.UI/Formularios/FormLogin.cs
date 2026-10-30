using Oficina.Negocio;

namespace Oficina.UI.Formularios;

internal class FormLogin : Form
{
    private readonly Aplicacion _app;
    private readonly TextBox _usuario;
    private readonly TextBox _clave;
    private readonly Label _error;

    public Sesion Sesion { get; private set; }

    public FormLogin(Aplicacion app)
    {
        _app = app;
        Text = "Registros de Oficina · Ingresar";
        Font = Estilo.Letra;
        BackColor = Estilo.Panel;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterScreen;
        MaximizeBox = false;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        Padding = new Padding(Estilo.Px(28), Estilo.Px(22), Estilo.Px(28), Estilo.Px(22));

        _usuario = new TextBox { Width = Estilo.Px(300), Font = new Font("Segoe UI", 11f), Margin = new Padding(0, 0, 0, Estilo.Px(10)) };
        _clave = new TextBox { Width = Estilo.Px(300), Font = new Font("Segoe UI", 11f), UseSystemPasswordChar = true, Margin = new Padding(0, 0, 0, Estilo.Px(6)) };
        _error = Estilo.Etiqueta("", color: Estilo.Peligro);
        _error.MaximumSize = new Size(Estilo.Px(300), 0);
        var entrar = Estilo.Boton("Ingresar", (_, _) => Entrar(), principal: true);
        entrar.Margin = new Padding(0, Estilo.Px(8), 0, 0);

        var tabla = new TableLayoutPanel { AutoSize = true, ColumnCount = 1, Dock = DockStyle.Fill };
        tabla.Controls.Add(Estilo.Etiqueta("Registros de Oficina", Estilo.Titulo));
        tabla.Controls.Add(Estilo.Etiqueta("Entre con la cuenta de su sector.", color: Estilo.TextoSuave));
        tabla.Controls.Add(Estilo.Etiqueta("Usuario"));
        tabla.Controls.Add(_usuario);
        tabla.Controls.Add(Estilo.Etiqueta("Contraseña"));
        tabla.Controls.Add(_clave);
        tabla.Controls.Add(_error);
        tabla.Controls.Add(entrar);
        tabla.Controls.Add(Estilo.Etiqueta($"Datos en {app.Origen}", new Font("Segoe UI", 8.25f), Estilo.TextoSuave));
        Controls.Add(tabla);
        AcceptButton = entrar;
    }

    private void Entrar()
    {
        try
        {
            Cursor.Current = Cursors.WaitCursor;
            Sesion = _app.Ingresar(_usuario.Text, _clave.Text);
            DialogResult = DialogResult.OK;
        }
        catch (ExcepcionNegocio ex)
        {
            _error.Text = ex.Message;
            _clave.SelectAll();
            _clave.Focus();
        }
        finally { Cursor.Current = Cursors.Default; }
    }
}
