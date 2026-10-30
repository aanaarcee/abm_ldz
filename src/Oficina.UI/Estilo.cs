using Oficina.Negocio;

namespace Oficina.UI;

/// fuente y colores
internal static class Estilo
{
    public static readonly Color Fondo = Color.FromArgb(233, 237, 242);
    public static readonly Color Panel = Color.White;
    public static readonly Color Texto = Color.FromArgb(17, 24, 39);
    public static readonly Color Menu = Color.FromArgb(15, 32, 56);
    public static readonly Color MenuActivo = Color.FromArgb(37, 64, 104);
    public static readonly Color Acento = Color.FromArgb(29, 78, 216);
    public static readonly Color TextoSuave = Color.FromArgb(55, 65, 81);
    public static readonly Color Borde = Color.FromArgb(176, 186, 199);
    public static readonly Color Encabezado = Color.FromArgb(214, 221, 231);
    public static readonly Color Seleccion = Color.FromArgb(29, 78, 216);
    public static readonly Color Resaltado = Color.FromArgb(254, 230, 160);
    public static readonly Color Peligro = Color.FromArgb(185, 28, 28);

    // organizamos que cada sector tenga un color para mejor organizacion y visibilidad
    // se elige por el número del sector, así un sector nuevo recibe color solo.
    // se usa como franja, borde o fondo suave: nunca como cuadradito con siglas.
    private static readonly Color[] ColoresSector =
    [
        Color.FromArgb(29, 78, 216),    // azul
        Color.FromArgb(4, 120, 87),     // verde
        Color.FromArgb(194, 65, 12),    // naranja
        Color.FromArgb(124, 58, 237),   // violeta
        Color.FromArgb(190, 18, 60),    // bordó
        Color.FromArgb(14, 116, 144),   // turquesa
        Color.FromArgb(161, 98, 7),     // ocre
        Color.FromArgb(71, 85, 105),    // pizarra
        Color.FromArgb(77, 124, 15),    // oliva
        Color.FromArgb(157, 23, 77),    // magenta oscuro
    ];

    public static Color ColorSector(int idSector) => ColoresSector[(Math.Max(idSector, 1) - 1) % ColoresSector.Length];

    /// funcion para mejorar el color de fondo para que se siga viendo el contenido
    public static Color FondoSector(int idSector) => Mezclar(ColorSector(idSector), Color.White, 0.86);

    public static Color Mezclar(Color c, Color con, double proporcion) => Color.FromArgb(
        (int)(c.R + (con.R - c.R) * proporcion), (int)(c.G + (con.G - c.G) * proporcion), (int)(c.B + (con.B - c.B) * proporcion));

    // colores para cada estado
    public static readonly Color Positivo = Color.FromArgb(21, 128, 61);
    public static readonly Color Pendiente = Color.FromArgb(180, 83, 9);

    public static Color ColorAccion(string accion) => accion switch
    {
        "Alta" => Positivo,
        "Modificación" => Pendiente,
        "Baja" => Peligro,
        _ => TextoSuave
    };

    /// los colores en la izquierda
    public static void FranjaIzquierda(Control c, Color color, int ancho = 5)
        => c.Paint += (_, e) => { using var b = new SolidBrush(color); e.Graphics.FillRectangle(b, 0, 0, Px(ancho), c.Height); };

    public static readonly Font Letra = new("Segoe UI", 9.75f);
    public static readonly Font Negrita = new("Segoe UI", 9.75f, FontStyle.Bold);
    public static readonly Font Titulo = new("Segoe UI Semibold", 15f);
    public static readonly Font Subtitulo = new("Segoe UI Semibold", 11.5f);
    public static readonly Font Numero = new("Segoe UI Semibold", 22f);

    private static float? _escala;
    /// funcion para los px, mejor ni tocar 
    public static int Px(int pixeles)
    {
        _escala ??= MedirEscala();
        return (int)Math.Round(pixeles * _escala.Value);
    }

    private static float MedirEscala()
    {
        using var g = Graphics.FromHwnd(IntPtr.Zero);
        return g.DpiX / 96f;
    }

    public static Button Boton(string texto, EventHandler alHacerClic = null, bool principal = false)
    {
        var b = new Button
        {
            Text = texto, AutoSize = true, FlatStyle = FlatStyle.Flat, Cursor = Cursors.Hand,
            Padding = new Padding(Px(8), Px(3), Px(8), Px(3)), Margin = new Padding(0, 0, Px(6), 0),
            BackColor = principal ? Acento : Panel, ForeColor = principal ? Color.White : Texto, UseVisualStyleBackColor = false,
            Font = principal ? Negrita : Letra
        };
        b.FlatAppearance.BorderColor = principal ? Acento : Borde;
        b.FlatAppearance.MouseOverBackColor = principal ? Mezclar(Acento, Color.Black, 0.2) : Encabezado;
        if (alHacerClic != null) b.Click += alHacerClic;
        return b;
    }

    public static Label Etiqueta(string texto, Font letra = null, Color? color = null) => new()
    {
        Text = texto, AutoSize = true, Font = letra ?? Letra, ForeColor = color ?? Texto, Margin = new Padding(0, Px(4), Px(6), Px(4))
    };

    ///  cajas grises que ayudan al diseño de los txtbox
    public static TextBox Buscador(string ayuda, int ancho) => new()
    {
        PlaceholderText = ayuda, Width = Px(ancho), Font = new Font("Segoe UI", 11f), BorderStyle = BorderStyle.FixedSingle
    };

    public static DataGridView Grilla()
    {
        var g = new DataGridView
        {
            Dock = DockStyle.Fill, ReadOnly = true, AllowUserToAddRows = false, AllowUserToDeleteRows = false, AllowUserToResizeRows = false,
            AutoGenerateColumns = false, SelectionMode = DataGridViewSelectionMode.FullRowSelect, MultiSelect = false,
            RowHeadersVisible = false, BackgroundColor = Panel, BorderStyle = BorderStyle.None, Font = Letra,
            CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal, GridColor = Borde,
            ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize, EnableHeadersVisualStyles = false,
            AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.None
        };
        g.RowTemplate.Height = Px(27);
        g.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.Single;
        g.ColumnHeadersDefaultCellStyle.BackColor = Encabezado;
        g.ColumnHeadersDefaultCellStyle.ForeColor = Texto;
        g.ColumnHeadersDefaultCellStyle.Font = Negrita;
        g.ColumnHeadersDefaultCellStyle.SelectionBackColor = Encabezado;
        g.ColumnHeadersDefaultCellStyle.Padding = new Padding(Px(2), Px(5), Px(2), Px(5));
        g.DefaultCellStyle.ForeColor = Texto;
        g.DefaultCellStyle.SelectionBackColor = Seleccion;
        g.DefaultCellStyle.SelectionForeColor = Color.White;
        g.DefaultCellStyle.FormatProvider = Formatos.Cultura;
        g.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(246, 248, 251);
        return g;
    }

    // diseño de los emcabezados
    public static void ColorearGrilla(DataGridView g, int idSector)
    {
        var color = ColorSector(idSector);
        g.ColumnHeadersDefaultCellStyle.BackColor = color;
        g.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
        g.ColumnHeadersDefaultCellStyle.SelectionBackColor = color;
        g.ColumnHeadersDefaultCellStyle.SelectionForeColor = Color.White;
        g.AlternatingRowsDefaultCellStyle.BackColor = Mezclar(color, Color.White, 0.94);
    }

    // diseños de marcos
    public static Panel Marco(Control c)
    {
        var p = new Panel { Dock = DockStyle.Fill, BackColor = Borde, Padding = new Padding(1) };
        c.Dock = DockStyle.Fill;
        p.Controls.Add(c);
        return p;
    }

    // agraga columnas
    public static DataGridViewTextBoxColumn Columna(this DataGridView g, string propiedad, string titulo, int ancho = 0, string formato = null)
    {
        var c = new DataGridViewTextBoxColumn
        {
            DataPropertyName = propiedad, HeaderText = titulo, Name = propiedad, SortMode = DataGridViewColumnSortMode.NotSortable
        };
        if (ancho > 0) c.Width = Px(ancho);
        else
        {
            c.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            c.MinimumWidth = Px(110);
        }
        if (formato != null)
        {
            c.DefaultCellStyle.Format = formato;
            if (formato.StartsWith('N')) c.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
        }
        g.Columns.Add(c);
        return c;
    }

    // lista en modo detalle. Una columna con ancho 0 se estira para ocupar el espacio que sobra
    public static ListView Lista(params (string Titulo, int Ancho)[] columnas)
    {
        var l = new ListView
        {
            Dock = DockStyle.Fill, View = View.Details, FullRowSelect = true, MultiSelect = false, HideSelection = false,
            Font = Letra, BorderStyle = BorderStyle.None, HeaderStyle = ColumnHeaderStyle.Nonclickable, ForeColor = Texto, GridLines = true
        };
        foreach (var (titulo, ancho) in columnas) l.Columns.Add(titulo, Px(ancho == 0 ? 200 : ancho));
        int relleno = Array.FindIndex(columnas, c => c.Ancho == 0);
        if (relleno >= 0)
            l.Resize += (_, _) =>
            {
                int otras = l.Columns.Cast<ColumnHeader>().Where(c => c.Index != relleno).Sum(c => c.Width);
                l.Columns[relleno].Width = Math.Max(Px(150), l.ClientSize.Width - otras - SystemInformation.VerticalScrollBarWidth);
            };
        return l;
    }

    public static string Fecha(DateTime f) => f.ToString("dd/MM/yyyy");
    public static string FechaHora(DateTime f) => f.ToString("dd/MM/yyyy HH:mm");
}
