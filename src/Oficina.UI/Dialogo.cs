using Oficina.Negocio;

namespace Oficina.UI;

// mensajes y preguntas cortas al usuario
internal static class Dialogo
{
    private const string Titulo = "Registros de Oficina";

    public static void Aviso(string texto) => MessageBox.Show(texto, Titulo, MessageBoxButtons.OK, MessageBoxIcon.Information);

    public static bool Confirmar(string texto)
        => MessageBox.Show(texto, Titulo, MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2) == DialogResult.Yes;

    // los errores de negocio se muestran como advertencias, los demás como errores
    public static void MostrarError(Exception ex)
    {
        Cursor.Current = Cursors.Default;
        if (ex is ExcepcionNegocio)
            MessageBox.Show(ex.Message, Titulo, MessageBoxButtons.OK, MessageBoxIcon.Warning);
        else
            MessageBox.Show($"Ocurrió un error inesperado:\n\n{ex.Message}", Titulo, MessageBoxButtons.OK, MessageBoxIcon.Error);
    }

    // pide un texto corto al usuario, con un valor inicial opcional y la opción de ocultar el texto (para contraseñas)
    public static string PedirTexto(IWin32Window dueño, string titulo, string pregunta, string inicial = "", bool clave = false)
    {
        using var f = new Form
        {
            Text = titulo, FormBorderStyle = FormBorderStyle.FixedDialog, StartPosition = FormStartPosition.CenterParent,
            MinimizeBox = false, MaximizeBox = false, ShowInTaskbar = false, Font = Estilo.Letra, AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink, Padding = new Padding(Estilo.Px(14)), BackColor = Estilo.Panel
        };
        var caja = new TextBox { Text = inicial, Width = Estilo.Px(360), UseSystemPasswordChar = clave, Margin = new Padding(0, 0, 0, Estilo.Px(12)) };
        var aceptar = Estilo.Boton("Aceptar", principal: true);
        aceptar.DialogResult = DialogResult.OK;
        var cancelar = Estilo.Boton("Cancelar");
        cancelar.DialogResult = DialogResult.Cancel;
        var botones = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.RightToLeft, Dock = DockStyle.Fill, Margin = Padding.Empty };
        botones.Controls.AddRange([cancelar, aceptar]);
        var tabla = new TableLayoutPanel { AutoSize = true, ColumnCount = 1, Dock = DockStyle.Fill };
        tabla.Controls.Add(Estilo.Etiqueta(pregunta));
        tabla.Controls.Add(caja);
        tabla.Controls.Add(botones);
        f.Controls.Add(tabla);
        f.AcceptButton = aceptar;
        f.CancelButton = cancelar;
        return f.ShowDialog(dueño) == DialogResult.OK ? caja.Text : null;
    }
}
