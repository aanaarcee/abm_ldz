using Oficina.UI.Vistas;

namespace Oficina.UI.Formularios;

/// <summary>Ventana de Configuración (solo para el Administrador): roles, permisos, cuentas, sectores, listas e historial.</summary>
internal class FormConfiguracion : Form
{
    public FormConfiguracion(INavegador nav)
    {
        nav.Sesion.ExigirAdministrador();   // aunque alguien llegue a abrirla, sin ser Administrador no se arma
        Text = "Configuración · Registros de Oficina";
        Font = Estilo.Letra;
        BackColor = Estilo.Fondo;
        StartPosition = FormStartPosition.CenterParent;
        Size = new Size(Estilo.Px(1100), Estilo.Px(720));
        MinimumSize = new Size(Estilo.Px(860), Estilo.Px(520));
        MinimizeBox = false;
        ShowInTaskbar = false;
        Padding = new Padding(Estilo.Px(18));

        var cerrar = Estilo.Boton("Cerrar", (_, _) => Close());
        var abajo = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom, AutoSize = true, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(0, Estilo.Px(10), 0, 0)
        };
        abajo.Controls.Add(cerrar);

        Controls.Add(new VistaAdministracion(nav) { Dock = DockStyle.Fill });
        Controls.Add(abajo);
        CancelButton = cerrar;
    }
}
