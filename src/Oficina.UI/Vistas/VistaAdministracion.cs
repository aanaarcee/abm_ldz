using System.ComponentModel;
using Oficina.Entidades;
using Oficina.Negocio;
using Oficina.UI.Formularios;

namespace Oficina.UI.Vistas;

// administración: roles y permisos por sector, cuentas, sectores, listas y el historial de estos cambios
internal class VistaAdministracion : UserControl
{
    private readonly ServicioAdministracion _admin;

    // roles y permisos
    private readonly ListBox _roles;
    private readonly CheckBox _esAdmin;
    private readonly DataGridView _permisos;
    private readonly Label _ayudaPermisos;
    private bool _cargando;

    // cuentas, sectores, listas, historial
    private readonly DataGridView _cuentas, _sectores;
    private readonly ListBox _reparticiones, _juzgados;
    private readonly ListView _historial;

    public VistaAdministracion(INavegador nav)
    {
        _admin = nav.App.Administracion(nav.Sesion);
        BackColor = Estilo.Fondo;

        var pestañas = new TabControl { Dock = DockStyle.Fill, Padding = new Point(Estilo.Px(12), Estilo.Px(5)) };

        // roles y permisos
        _roles = new ListBox { Dock = DockStyle.Fill, Font = Estilo.Letra, IntegralHeight = false, BorderStyle = BorderStyle.FixedSingle };
        _roles.SelectedIndexChanged += (_, _) => CargarPermisos();
        var botonesRol = Fila(Estilo.Boton("Nuevo rol", (_, _) => NuevoRol()), Estilo.Boton("Renombrar", (_, _) => RenombrarRol()));
        var izquierda = Columna(Estilo.Etiqueta("Roles (uno por departamento)", Estilo.Negrita), botonesRol);
        izquierda.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        izquierda.Controls.Add(_roles);

        _esAdmin = new CheckBox { Text = "Administrador: puede todo en todos los sectores", AutoSize = true, Font = Estilo.Letra };
        _esAdmin.CheckedChanged += (_, _) => CambiarAdministrador();
        _ayudaPermisos = Estilo.Etiqueta("", color: Estilo.TextoSuave);
        _permisos = Estilo.Grilla();
        _permisos.ReadOnly = false;
        _permisos.Columna(nameof(FilaPermiso.Sector), "Sector", 220).ReadOnly = true;   // Sector.ToString() es el nombre
        foreach (var (prop, titulo) in new[] { ("Leer", "Ver"), ("Cargar", "Cargar"), ("Modificar", "Modificar"), ("Eliminar", "Eliminar") })
            _permisos.Columns.Add(new DataGridViewCheckBoxColumn { DataPropertyName = prop, HeaderText = titulo, Name = prop, Width = Estilo.Px(90) });
        ColorearSector(_permisos, nameof(FilaPermiso.Sector), x => (x as FilaPermiso)?.Sector.IdSector);
        _permisos.CurrentCellDirtyStateChanged += (_, _) => { if (_permisos.IsCurrentCellDirty) _permisos.CommitEdit(DataGridViewDataErrorContexts.Commit); };
        var guardarPermisos = Estilo.Boton("Guardar permisos", (_, _) => GuardarPermisos(), principal: true);
        var derecha = Columna(_esAdmin, _ayudaPermisos, guardarPermisos);
        derecha.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        derecha.Controls.Add(Marco(_permisos));

        var division = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2 };
        division.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, Estilo.Px(260)));
        division.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        division.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        derecha.Padding = new Padding(Estilo.Px(12), 0, 0, 0);
        division.Controls.Add(izquierda, 0, 0);
        division.Controls.Add(derecha, 1, 0);
        Pestaña(pestañas, "Roles y permisos", division);

        // cuentas
        _cuentas = Estilo.Grilla();
        _cuentas.Columna("NombreUsuario", "Cuenta", 180);
        _cuentas.Columna("Rol", "Rol", 220);
        _cuentas.Columna("Estado", "Estado");
        var cuentas = Columna(
            Estilo.Etiqueta("Una cuenta general por sector. Las contraseñas se guardan cifradas (PBKDF2): no se pueden ver, solo cambiar.", color: Estilo.TextoSuave),
            Fila(Estilo.Boton("Nueva cuenta", (_, _) => NuevaCuenta(), principal: true), Estilo.Boton("Cambiar contraseña", (_, _) => CambiarClave()),
                 Estilo.Boton("Cambiar rol o estado", (_, _) => CambiarRol())));
        cuentas.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        cuentas.Controls.Add(Marco(_cuentas));
        Pestaña(pestañas, "Cuentas", cuentas);

        // sectores
        _sectores = Estilo.Grilla();
        _sectores.Columna("Nombre", "Sector", 240);
        _sectores.Columna("Clave", "Clave", 80);
        _sectores.Columna("Tipo", "Qué registra");
        ColorearSector(_sectores, "Nombre", x => (x as Sector)?.IdSector);
        _sectores.CellFormatting += (_, e) =>
        {
            if (_sectores.Columns[e.ColumnIndex].Name == "Tipo" && e.Value is TipoSector t) { e.Value = NombreTipo(t); e.FormattingApplied = true; }
        };
        var sectores = Columna(
            Estilo.Etiqueta("Los sectores nuevos aparecen en el menú al cerrar Configuración. Después, dé permisos al rol que corresponda.", color: Estilo.TextoSuave),
            Fila(Estilo.Boton("Nuevo sector", (_, _) => EditarSector(null), principal: true),
                 Estilo.Boton("Modificar", (_, _) => { if (_sectores.CurrentRow?.DataBoundItem is Sector s) EditarSector(s); })));
        sectores.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        sectores.Controls.Add(Marco(_sectores));
        Pestaña(pestañas, "Sectores", sectores);

        // listas
        _reparticiones = new ListBox { Dock = DockStyle.Fill, Font = Estilo.Letra, IntegralHeight = false };
        _juzgados = new ListBox { Dock = DockStyle.Fill, Font = Estilo.Letra, IntegralHeight = false };
        var listas = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2 };
        listas.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        listas.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        listas.Controls.Add(ListaEditable("Reparticiones", _reparticiones,
            () => EditarNombre("Nueva repartición", "", n => _admin.GuardarReparticion(new Reparticion { Nombre = n })),
            () => { if (_reparticiones.SelectedItem is Reparticion r) EditarNombre("Renombrar repartición", r.Nombre, n => _admin.GuardarReparticion(new Reparticion { IdReparticion = r.IdReparticion, Nombre = n })); }));
        listas.Controls.Add(ListaEditable("Juzgados", _juzgados,
            () => EditarNombre("Nuevo juzgado", "", n => _admin.GuardarJuzgado(new Juzgado { Nombre = n })),
            () => { if (_juzgados.SelectedItem is Juzgado j) EditarNombre("Renombrar juzgado", j.Nombre, n => _admin.GuardarJuzgado(new Juzgado { IdJuzgado = j.IdJuzgado, Nombre = n })); }));
        Pestaña(pestañas, "Listas", listas);

        // historial
        _historial = Estilo.Lista(("Fecha", 130), ("Cuenta", 110), ("Qué", 100), ("Acción", 110), ("Detalle", 0));
        Pestaña(pestañas, "Historial de administración", Marco(_historial));
        pestañas.SelectedIndexChanged += (_, _) => { if (pestañas.SelectedIndex == 4) CargarHistorial(); };

        var tabla = Columna(Estilo.Etiqueta("Configuración", Estilo.Titulo),
            Estilo.Etiqueta("Los cambios de permisos de otras cuentas se aplican cuando esas cuentas vuelven a entrar.", color: Estilo.TextoSuave));
        tabla.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        tabla.Controls.Add(pestañas);
        Controls.Add(tabla);

        CargarRoles();
        CargarCuentas();
        CargarSectores();
        CargarListas();
    }

    // armado

    private static TableLayoutPanel Columna(params Control[] controles)
    {
        var t = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1 };
        foreach (var c in controles)
        {
            t.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            t.Controls.Add(c);
        }
        return t;
    }

    private static FlowLayoutPanel Fila(params Control[] controles)
    {
        var f = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, WrapContents = false, Margin = new Padding(0, Estilo.Px(6), 0, Estilo.Px(6)) };
        f.Controls.AddRange(controles);
        return f;
    }

    private static Panel Marco(Control c) => Estilo.Marco(c);

    // funcion de diseño para las grillas
    private static void ColorearSector(DataGridView g, string columna, Func<object, int?> idSector)
        => g.CellFormatting += (_, e) =>
        {
            if (e.RowIndex < 0 || g.Columns[e.ColumnIndex].Name != columna || idSector(g.Rows[e.RowIndex].DataBoundItem) is not int id) return;
            e.CellStyle.ForeColor = e.CellStyle.SelectionForeColor = Estilo.ColorSector(id);
            e.CellStyle.BackColor = e.CellStyle.SelectionBackColor = Estilo.FondoSector(id);
            e.CellStyle.Font = Estilo.Negrita;
        };

    private static void Pestaña(TabControl t, string titulo, Control contenido)
    {
        var p = new TabPage(titulo) { BackColor = Estilo.Fondo, Padding = new Padding(Estilo.Px(10)) };
        p.Controls.Add(contenido);
        t.TabPages.Add(p);
    }

    private static Control ListaEditable(string titulo, ListBox lista, Action agregar, Action renombrar)
    {
        var c = Columna(Estilo.Etiqueta(titulo, Estilo.Negrita), Fila(Estilo.Boton("Agregar", (_, _) => agregar()), Estilo.Boton("Renombrar", (_, _) => renombrar())));
        c.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        c.Controls.Add(lista);
        c.Padding = new Padding(0, 0, Estilo.Px(12), 0);
        return c;
    }

    private static string NombreTipo(TipoSector t) => t switch
    {
        TipoSector.Actas => "Movimientos de actas",
        TipoSector.Reclamos => "Reclamos (autos abandonados)",
        TipoSector.Lotes => "Lotes",
        _ => "Solo consulta"
    };

    // roles y permisos

    private void CargarRoles(int? elegir = null)
    {
        var anterior = elegir ?? (_roles.SelectedItem as Rol)?.IdRol;
        _roles.DataSource = _admin.Roles();
        if (anterior.HasValue)
            for (int i = 0; i < _roles.Items.Count; i++)
                if (((Rol)_roles.Items[i]).IdRol == anterior) _roles.SelectedIndex = i;
        CargarPermisos();
    }

    private void CargarPermisos()
    {
        if (_roles.SelectedItem is not Rol rol) return;
        _cargando = true;
        _esAdmin.Checked = rol.EsAdministrador;
        _esAdmin.Enabled = rol.IdSectorPropio == null || rol.EsAdministrador;   // un rol de sector no puede ser Administrador
        _cargando = false;
        _permisos.DataSource = new BindingList<FilaPermiso>(_admin.Permisos(rol.IdRol));
        _permisos.Enabled = !rol.EsAdministrador;
        _ayudaPermisos.Text = rol.EsAdministrador
            ? "El Administrador puede todo: no necesita permisos por sector."
            : "Marque qué puede hacer este rol en cada sector. Cargar, modificar o eliminar incluyen ver.";
    }

    private void CambiarAdministrador()
    {
        if (_cargando || _roles.SelectedItem is not Rol rol) return;
        if (_esAdmin.Checked && !Dialogo.Confirmar($"¿Hacer Administrador al rol {rol.Nombre}? Va a poder ver y cambiar todo, incluida la Configuración."))
        {
            CargarPermisos();
            return;
        }
        rol.EsAdministrador = _esAdmin.Checked;
        try { _admin.GuardarRol(rol); }
        finally { CargarRoles(rol.IdRol); }
    }

    private void GuardarPermisos()
    {
        if (_roles.SelectedItem is not Rol rol) return;
        _permisos.EndEdit();
        _admin.GuardarPermisos(rol.IdRol, (BindingList<FilaPermiso>)_permisos.DataSource);
        CargarPermisos();
        Dialogo.Aviso($"Se guardaron los permisos de {rol.Nombre}.");
    }

    private void NuevoRol()
    {
        var nombre = Dialogo.PedirTexto(this, "Nuevo rol", "Nombre del rol (por ejemplo, el departamento):");
        if (nombre == null) return;
        var rol = new Rol { Nombre = nombre };
        _admin.GuardarRol(rol);
        CargarRoles(rol.IdRol);
    }

    private void RenombrarRol()
    {
        if (_roles.SelectedItem is not Rol rol) return;
        var nombre = Dialogo.PedirTexto(this, "Renombrar rol", "Nuevo nombre:", rol.Nombre);
        if (nombre == null) return;
        rol.Nombre = nombre;
        _admin.GuardarRol(rol);
        CargarRoles(rol.IdRol);
    }

    // administracion de cuentas

    private void CargarCuentas()
    {
        var roles = _admin.Roles().ToDictionary(r => r.IdRol, r => r.Nombre);
        _cuentas.DataSource = _admin.Usuarios().Select(u => new FilaCuenta
        {
            IdUsuario = u.IdUsuario, IdRol = u.IdRol, Activo = u.Activo, NombreUsuario = u.NombreUsuario,
            Rol = roles.GetValueOrDefault(u.IdRol), Estado = u.Activo ? "Activa" : "Desactivada"
        }).ToList();
    }

    private class FilaCuenta
    {
        public int IdUsuario { get; set; }
        public int IdRol { get; set; }
        public bool Activo { get; set; }
        public string NombreUsuario { get; set; }
        public string Rol { get; set; }
        public string Estado { get; set; }
    }

    private void NuevaCuenta()
    {
        using var f = new FormCuenta(_admin, null);
        if (f.ShowDialog(this) == DialogResult.OK) CargarCuentas();
    }

    private void CambiarRol()
    {
        if (_cuentas.CurrentRow?.DataBoundItem is not FilaCuenta c) return;
        using var f = new FormCuenta(_admin, new Usuario { IdUsuario = c.IdUsuario, NombreUsuario = c.NombreUsuario, IdRol = c.IdRol, Activo = c.Activo });
        if (f.ShowDialog(this) == DialogResult.OK) CargarCuentas();
    }

    private void CambiarClave()
    {
        if (_cuentas.CurrentRow?.DataBoundItem is not FilaCuenta c) return;
        var clave = Dialogo.PedirTexto(this, "Cambiar contraseña", $"Nueva contraseña para «{c.NombreUsuario}» (al menos 6 caracteres):", clave: true);
        if (clave == null) return;
        _admin.CambiarClave(c.IdUsuario, clave);
        Dialogo.Aviso("Contraseña cambiada.");
    }

    // administracion de sectores y listas

    private void CargarSectores() => _sectores.DataSource = _admin.Sectores();

    private void EditarSector(Sector s)
    {
        using var f = new FormSector(_admin, s);
        if (f.ShowDialog(this) != DialogResult.OK) return;
        CargarSectores();
        CargarPermisos();
    }

    private void CargarListas()
    {
        _reparticiones.DataSource = _admin.Reparticiones();
        _juzgados.DataSource = _admin.Juzgados();
    }

    private void EditarNombre(string titulo, string actual, Action<string> guardar)
    {
        var nombre = Dialogo.PedirTexto(this, titulo, "Nombre:", actual);
        if (nombre == null) return;
        guardar(nombre);
        CargarListas();
    }

    private void CargarHistorial()
    {
        _historial.BeginUpdate();
        _historial.Items.Clear();
        foreach (var h in _admin.HistorialAdministracion())
            _historial.Items.Add(new ListViewItem([Estilo.FechaHora(h.Fecha), h.Usuario, h.Entidad, h.Accion, h.Detalle]));
        _historial.EndUpdate();
    }
}

// altas en general
internal class FormCuenta : FormularioDatos
{
    private readonly ServicioAdministracion _admin;
    private readonly Usuario _existente;
    private readonly TextBox _nombre, _clave;
    private readonly ComboBox _rol;
    private readonly CheckBox _activa;

    public FormCuenta(ServicioAdministracion admin, Usuario existente) : base(existente == null ? "Nueva cuenta" : $"Cuenta {existente.NombreUsuario}")
    {
        _admin = admin;
        _existente = existente;
        Seccion(existente == null ? "Nueva cuenta de sector" : "Rol y estado");
        if (existente == null)
        {
            _nombre = Campo("Usuario", Texto(30));
            _clave = Campo("Contraseña", new TextBox { UseSystemPasswordChar = true });
            Nota("Al menos 6 caracteres. Se guarda cifrada.");
        }
        _rol = Campo("Rol", Lista(admin.Roles()));
        if (existente != null)
        {
            Elegir<Rol>(_rol, r => r.IdRol == existente.IdRol);
            _activa = Campo("", new CheckBox { Text = "Cuenta activa (puede entrar)", AutoSize = true, Checked = existente.Activo });
        }
    }

    protected override bool Guardar()
    {
        int idRol = (_rol.SelectedItem as Rol)?.IdRol ?? 0;
        if (_existente == null) _admin.CrearUsuario(_nombre.Text, _clave.Text, idRol);
        else _admin.CambiarRolYEstado(_existente.IdUsuario, idRol, _activa.Checked);
        return true;
    }
}

// altas y modificaciones
internal class FormSector : FormularioDatos
{
    private readonly ServicioAdministracion _admin;
    private readonly Sector _existente;
    private readonly TextBox _nombre, _clave;
    private readonly ComboBox _tipo;

    private record OpcionTipo(TipoSector Tipo, string Texto)
    {
        public override string ToString() => Texto;
    }

    public FormSector(ServicioAdministracion admin, Sector existente) : base(existente == null ? "Nuevo sector" : $"Sector {existente.Nombre}")
    {
        _admin = admin;
        _existente = existente;
        Seccion("Sector");
        _nombre = Campo("Nombre", Texto(60));
        _clave = Campo("Clave (hasta 4 letras)", Texto(4));
        _tipo = Campo("Qué registra", Lista(new[]
        {
            new OpcionTipo(TipoSector.Actas, "Movimientos de actas"), new OpcionTipo(TipoSector.Reclamos, "Reclamos (autos abandonados)"),
            new OpcionTipo(TipoSector.Lotes, "Lotes"), new OpcionTipo(TipoSector.Consulta, "Solo consulta")
        }));
        if (existente != null)
        {
            _nombre.Text = existente.Nombre;
            _clave.Text = existente.Clave;
            Elegir<OpcionTipo>(_tipo, o => o.Tipo == existente.Tipo);
        }
    }

    protected override bool Guardar()
    {
        _admin.GuardarSector(new Sector
        {
            IdSector = _existente?.IdSector ?? 0, Nombre = _nombre.Text, Clave = _clave.Text, Tipo = ((OpcionTipo)_tipo.SelectedItem).Tipo
        });
        return true;
    }
}
