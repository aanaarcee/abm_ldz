using Oficina.Entidades;
using Oficina.Negocio;
using Oficina.UI.Vistas;

namespace Oficina.UI.Formularios;

/// <summary>Lo que las vistas necesitan de la ventana principal para moverse entre pantallas.</summary>
internal interface INavegador
{
    Aplicacion App { get; }
    Sesion Sesion { get; }
    void IrAInicio();
    void AbrirSector(int idSector, string filtro = null);
    void AbrirActa(int idActa);
}

/// <summary>Ventana principal: buscador general arriba, menú de sectores a la izquierda y la pantalla elegida a la derecha.</summary>
internal class FormPrincipal : Form, INavegador
{
    private readonly Panel _contenido;
    private readonly FlowLayoutPanel _menu;
    private readonly TextBox _buscador;
    private readonly System.Windows.Forms.Timer _espera = new() { Interval = 300 };
    private readonly VistaResultados _resultados;
    private Control _vistaAnterior;
    private Button _botonActivo;
    private bool _silenciar;

    public Aplicacion App { get; }
    public Sesion Sesion { get; private set; }
    /// <summary>Verdadero si se cerró con "Cambiar de cuenta" (vuelve al ingreso).</summary>
    public bool CambiarDeCuenta { get; private set; }

    public FormPrincipal(Aplicacion app, Sesion sesion)
    {
        App = app;
        Sesion = sesion;
        Text = $"Registros de Oficina · {sesion.Rol.Nombre}";
        Font = Estilo.Letra;
        BackColor = Estilo.Fondo;
        StartPosition = FormStartPosition.CenterScreen;
        Size = new Size(Estilo.Px(1280), Estilo.Px(780));
        MinimumSize = new Size(Estilo.Px(960), Estilo.Px(600));
        WindowState = FormWindowState.Maximized;

        // ---------- Barra de arriba con el buscador general ----------
        _buscador = Estilo.Buscador("Buscar en todo: N° de causa, acta, persona, DNI, remito, caso, lote…", 560);
        _buscador.Anchor = AnchorStyles.Left;
        _buscador.TextChanged += (_, _) => { _espera.Stop(); if (!_silenciar) _espera.Start(); };
        _buscador.KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Escape) { _buscador.Clear(); e.SuppressKeyPress = true; }
            if (e.KeyCode == Keys.Enter) { _espera.Stop(); Buscar(); e.SuppressKeyPress = true; }
        };
        _espera.Tick += (_, _) => { _espera.Stop(); Buscar(); };

        var usuario = Estilo.Etiqueta($"{sesion.Usuario.NombreUsuario} · {sesion.Rol.Nombre}", color: Color.White);
        usuario.Anchor = AnchorStyles.Right;
        var salir = Estilo.Boton("Cambiar de cuenta", (_, _) => { CambiarDeCuenta = true; Close(); });
        salir.Anchor = AnchorStyles.Right;

        var barra = new TableLayoutPanel
        {
            Dock = DockStyle.Top, Height = Estilo.Px(58), ColumnCount = 5, BackColor = Estilo.Menu,
            Padding = new Padding(Estilo.Px(16), Estilo.Px(12), Estilo.Px(16), Estilo.Px(10))
        };
        barra.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, Estilo.Px(214)));
        barra.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        barra.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        barra.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        barra.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        var titulo = Estilo.Etiqueta("Registros de Oficina", Estilo.Subtitulo, Color.White);
        titulo.Anchor = AnchorStyles.Left;
        barra.Controls.Add(titulo, 0, 0);
        barra.Controls.Add(_buscador, 1, 0);
        barra.Controls.Add(usuario, 2, 0);
        // La Configuración (roles, permisos, cuentas, sectores) solo existe para el Administrador
        if (sesion.EsAdministrador)
        {
            var configuracion = Estilo.Boton("⚙  Configuración", (_, _) => AbrirConfiguracion());
            configuracion.Anchor = AnchorStyles.Right;
            barra.Controls.Add(configuracion, 3, 0);
        }
        barra.Controls.Add(salir, 4, 0);

        // ---------- Menú lateral: Inicio y los sectores ----------
        _menu = new FlowLayoutPanel
        {
            Dock = DockStyle.Left, Width = Estilo.Px(230), BackColor = Estilo.Menu, FlowDirection = FlowDirection.TopDown,
            WrapContents = false, AutoScroll = true, Padding = new Padding(0, Estilo.Px(8), 0, 0)
        };
        ArmarMenu();

        _contenido = new Panel { Dock = DockStyle.Fill, Padding = new Padding(Estilo.Px(20)), BackColor = Estilo.Fondo };
        _resultados = new VistaResultados(this) { Dock = DockStyle.Fill };

        Controls.Add(_contenido);
        Controls.Add(_menu);
        Controls.Add(barra);

        Shown += (_, _) => { IrAInicio(); _buscador.Focus(); };
        KeyPreview = true;
        KeyDown += (_, e) =>
        {
            if (e.Control && e.KeyCode == Keys.F) { _buscador.Focus(); _buscador.SelectAll(); e.SuppressKeyPress = true; }
        };
    }

    private void ArmarMenu()
    {
        _menu.SuspendLayout();
        foreach (Control c in _menu.Controls.Cast<Control>().ToList()) c.Dispose();
        _botonActivo = null;
        AgregarAlMenu("Inicio", IrAInicio);
        AgregarSeparador("SECTORES");
        foreach (var s in Sesion.SectoresVisibles)
        {
            var id = s.IdSector;
            var b = AgregarAlMenu(s.Nombre, () => AbrirSector(id));
            b.Tag = id;
            Estilo.FranjaIzquierda(b, Estilo.Mezclar(Estilo.ColorSector(id), Color.White, 0.25), 6);
        }
        _menu.ResumeLayout();
    }

    /// <summary>Abre la ventana de Configuración y, al cerrarla, vuelve a leer sectores y permisos.</summary>
    private void AbrirConfiguracion()
    {
        using (var f = new FormConfiguracion(this)) f.ShowDialog(this);
        Sesion = App.Recargar(Sesion);
        ArmarMenu();
        IrAInicio();
    }

    private Button AgregarAlMenu(string texto, Action accion)
    {
        var b = new Button
        {
            Text = "   " + texto, TextAlign = ContentAlignment.MiddleLeft, FlatStyle = FlatStyle.Flat, ForeColor = Color.White,
            BackColor = Estilo.Menu, Width = Estilo.Px(230), Height = Estilo.Px(38), Margin = Padding.Empty, Cursor = Cursors.Hand,
            Font = new Font("Segoe UI", 10.5f), UseVisualStyleBackColor = false
        };
        b.FlatAppearance.BorderSize = 0;
        b.FlatAppearance.MouseOverBackColor = Estilo.MenuActivo;
        b.Click += (_, _) =>
        {
            LimpiarBusqueda();
            accion();
            Marcar(b);
        };
        _menu.Controls.Add(b);
        return b;
    }

    private void AgregarSeparador(string texto)
    {
        var l = Estilo.Etiqueta(texto, new Font("Segoe UI", 8.25f, FontStyle.Bold), Color.FromArgb(190, 205, 225));
        l.Margin = new Padding(Estilo.Px(14), Estilo.Px(16), 0, Estilo.Px(4));
        _menu.Controls.Add(l);
    }

    private void Marcar(Button b)
    {
        if (_botonActivo != null) _botonActivo.BackColor = Estilo.Menu;
        _botonActivo = b;
        if (b != null) b.BackColor = Estilo.MenuActivo;
    }

    private void Mostrar(Control vista)
    {
        if (_contenido.Controls.Count > 0 && _contenido.Controls[0] == vista) return;
        foreach (Control c in _contenido.Controls.Cast<Control>().ToList())
        {
            _contenido.Controls.Remove(c);
            if (c != _resultados) c.Dispose();
        }
        vista.Dock = DockStyle.Fill;
        _contenido.Controls.Add(vista);
    }

    // ---------- Búsqueda general (estilo WhatsApp: mientras se escribe) ----------

    private void Buscar()
    {
        var texto = _buscador.Text.Trim();
        bool mostrando = _contenido.Controls.Count > 0 && _contenido.Controls[0] == _resultados;
        if (texto.Length < ServicioBusqueda.MinimoCaracteres)
        {
            if (mostrando)
            {
                _contenido.Controls.Remove(_resultados);
                if (_vistaAnterior != null && !_vistaAnterior.IsDisposed)
                {
                    _contenido.Controls.Add(_vistaAnterior);
                    if (_vistaAnterior is IRefrescable r) r.Refrescar();
                }
                else Mostrar(new VistaInicio(this));
                _vistaAnterior = null;
            }
            return;
        }
        if (!mostrando)
        {
            _vistaAnterior = _contenido.Controls.Count > 0 ? _contenido.Controls[0] : null;
            _contenido.Controls.Clear();   // la vista anterior se guarda para volver al borrar la búsqueda
            _contenido.Controls.Add(_resultados);
            Marcar(null);
        }
        _resultados.Mostrar(texto, App.Busqueda(Sesion).Buscar(texto));
    }

    // ---------- INavegador ----------

    /// <summary>Borra el buscador general sin disparar otra búsqueda y descarta la vista guardada.</summary>
    private void LimpiarBusqueda()
    {
        _espera.Stop();
        _silenciar = true;
        _buscador.Text = "";
        _silenciar = false;
        if (_vistaAnterior != null && _vistaAnterior.Parent == null) _vistaAnterior.Dispose();
        _vistaAnterior = null;
    }

    public void IrAInicio()
    {
        LimpiarBusqueda();
        Mostrar(new VistaInicio(this));
        Marcar(_menu.Controls.OfType<Button>().FirstOrDefault());
    }

    public void AbrirSector(int idSector, string filtro = null)
    {
        LimpiarBusqueda();
        Mostrar(new VistaSector(this, Sesion.Sector(idSector), filtro));
        Marcar(_menu.Controls.OfType<Button>().FirstOrDefault(b => b.Tag is int id && id == idSector));
    }

    public void AbrirActa(int idActa)
    {
        using var ficha = new FormActa(this, idActa);
        ficha.ShowDialog(this);
        if (ficha.HuboCambios && _contenido.Controls.Count > 0 && _contenido.Controls[0] is IRefrescable r) r.Refrescar();
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        _espera.Dispose();
        _resultados.Dispose();
        if (_vistaAnterior != null && !_vistaAnterior.IsDisposed) _vistaAnterior.Dispose();
        base.OnFormClosed(e);
    }
}

/// <summary>Vistas que se pueden volver a cargar después de un cambio.</summary>
internal interface IRefrescable
{
    void Refrescar();
}
