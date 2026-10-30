using Oficina.Datos;
using Oficina.Entidades;

namespace Oficina.Negocio;

// todo lo que ve una ficha de acta
public class FichaActa
{
    public Acta Acta { get; set; }
    public Persona Persona { get; set; }
    public List<Movimiento> Recorrido { get; set; }
    public List<ActaVersion> Versiones { get; set; }
    public List<FilaComparacion> Comparacion { get; set; }
    public List<EntradaHistorial> Historial { get; set; }
    public bool PuedeModificar { get; set; }

    public ActaVersion Original => Versiones.FirstOrDefault();
}

public class FilaComparacion
{
    public string Campo { get; set; }
    public string Original { get; set; }
    public string Actual { get; set; }
    public bool Cambio => Original != Actual;
}

// Alta, modificación y baja de lo que anota cada sector. Valida, controla permisos y escribe el historial
public class ServicioRegistros : ServicioBase
{
    public ServicioRegistros(Sesion sesion, IDatosRegistros datos) : base(sesion, datos) { }

    public List<Reparticion> Reparticiones() => Datos.ListarReparticiones();
    public List<Juzgado> Juzgados() => Datos.ListarJuzgados();

    //  Grillas por sector (con el buscador propio del sector)

    public List<Movimiento> Movimientos(int idSector, string filtro = null)
    {
        Sesion.Exigir(idSector, Accion.Leer);
        var t = filtro?.Trim();
        return Datos.ListarMovimientos(idSector).Where(m => string.IsNullOrEmpty(t)
            || m.Anio.ToString() == t
            || TextoBusqueda.AlgunoContiene(t, m.NroCausa, m.Reparticion, m.NroActa, m.Persona, m.Documento, m.Detalle, m.Remito, m.Observaciones)).ToList();
    }

    public List<Reclamo> Reclamos(int idSector, string filtro = null)
    {
        Sesion.Exigir(idSector, Accion.Leer);
        var t = filtro?.Trim();
        return Datos.ListarReclamos().Where(r => string.IsNullOrEmpty(t)
            || TextoBusqueda.AlgunoContiene(t, r.NroCaso, r.Ubicacion, r.Vehiculo, r.VerificadoPor, r.Observaciones,
                                           r.Verificado ? "verificado" : "sin verificar")).ToList();
    }

    public List<Lote> Lotes(int idSector, string filtro = null)
    {
        Sesion.Exigir(idSector, Accion.Leer);
        var t = filtro?.Trim();
        return Datos.ListarLotes().Where(l => string.IsNullOrEmpty(t) || TextoBusqueda.AlgunoContiene(t, l.NroLote, l.Observaciones)).ToList();
    }

    // Actas y movimientos

    // Sugerencias que no nombran sectores (por ahora el movimiento es texto libre)
    private static readonly string[] SugerenciasGenerales =
    [
        "Ingreso de acta", "Recepción de acta por remito", "Control de plazos", "Pedido de informe", "Auditoría de acta",
        "Observada", "Sentencia dictada", "Notificación de sentencia", "Apelación presentada",
        "Inicio de juicio de apremio", "Plan de pago acordado", "Embargo trabado", "Archivo"
    ];

 
    /// Sugerencias para el campo Movimiento: las generales, "Pase a …" solo hacia sectores que la cuenta ve,
    // y los movimientos que ya se usaron en este sector.(no entendi nada)
   
    public List<string> SugerenciasDeMovimiento(int idSector)
    {
        Sesion.Exigir(idSector, Accion.Leer);
        var pases = Sesion.SectoresVisibles.Where(s => s.Tipo == TipoSector.Actas && s.IdSector != idSector).Select(s => $"Pase a {s.Nombre}");
        var usados = Datos.ListarMovimientos(idSector).Select(m => m.Detalle);
        return SugerenciasGenerales.Concat(pases).Concat(usados)
            .Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.CurrentCultureIgnoreCase).OrderBy(x => x).ToList();
    }

    // Para el formulario de carga: si el acta ya existe, se completan sus datos solos
    public Acta BuscarActa(string nroCausa, int idReparticion, int anio)
        => string.IsNullOrWhiteSpace(nroCausa) || idReparticion == 0 || anio == 0 ? null : Datos.BuscarActa(nroCausa.Trim(), idReparticion, anio);

    public Movimiento ObtenerMovimiento(int idMovimiento)
    {
        var m = Datos.ObtenerMovimiento(idMovimiento) ?? throw new ExcepcionNegocio("El movimiento no existe.");
        Sesion.Exigir(m.IdSector, Accion.Leer);
        return m;
    }

    
    /// Anota un movimiento en el sector. Si el acta (causa + repartición + año) no existe, la da de alta como versión original.
    /// Si ya existe, se usa tal cual: para cambiar sus datos está "Modificar acta" en la ficha.
    public Movimiento CargarMovimiento(int idSector, Acta acta, Persona persona, Movimiento movimiento)
    {
        Sesion.Exigir(idSector, Accion.Cargar);
        if (Sesion.Sector(idSector).Tipo != TipoSector.Actas) throw new ExcepcionNegocio("En este sector no se anotan actas.");
        ValidarClave(acta);
        ValidarMovimiento(movimiento);

        var existente = Datos.BuscarActa(acta.NroCausa, acta.IdReparticion, acta.Anio);
        if (existente == null)
        {
            ValidarActa(acta);
            ValidarPersona(persona);
            Guardar(() =>
            {
                acta.IdPersona = ResolverPersona(persona);
                acta.Version = 1;
                acta.Eliminado = false;
                acta.IdUsuarioAlta = Sesion.IdUsuario;
                acta.FechaAlta = DateTime.Now;
                acta.IdUsuarioModif = null;
                acta.FechaModif = null;
                Datos.GuardarActa(acta);
                Datos.AgregarVersion(ActaVersion.Desde(acta, 1, "Acta original", Sesion.IdUsuario, acta.FechaAlta));
            });
            Registrar("Acta", acta.IdActa, idSector, "Alta", $"Causa {acta.NroCausa} · acta {acta.NroActa}");
        }
        else if (existente.Eliminado) throw new ExcepcionNegocio("Esa acta está dada de baja.");
        else acta = existente;

        movimiento.IdMovimiento = 0;
        movimiento.IdActa = acta.IdActa;
        movimiento.IdSector = idSector;
        movimiento.Eliminado = false;
        movimiento.IdUsuarioAlta = Sesion.IdUsuario;
        movimiento.FechaAlta = DateTime.Now;
        Guardar(() => Datos.GuardarMovimiento(movimiento));
        Registrar("Movimiento", movimiento.IdMovimiento, idSector, "Alta", $"Causa {acta.NroCausa}: {movimiento.Detalle}");
        return Datos.ObtenerMovimiento(movimiento.IdMovimiento);
    }

    public void ModificarMovimiento(Movimiento cambios)
    {
        var actual = Datos.ObtenerMovimiento(cambios.IdMovimiento) ?? throw new ExcepcionNegocio("El movimiento no existe.");
        Sesion.Exigir(actual.IdSector, Accion.Modificar);
        if (actual.Eliminado) throw new ExcepcionNegocio("El movimiento está dado de baja.");
        ValidarMovimiento(cambios);

        var detalle = new Cambios()
            .Campo("Fecha", actual.Fecha, cambios.Fecha).Campo("Movimiento", actual.Detalle, Limpio(cambios.Detalle))
            .Campo("Remito", actual.Remito, Limpio(cambios.Remito)).Campo("Observaciones", actual.Observaciones, Limpio(cambios.Observaciones));
        if (!detalle.Hay) return;

        actual.Fecha = cambios.Fecha.Date;
        actual.Detalle = Limpio(cambios.Detalle);
        actual.Remito = Limpio(cambios.Remito);
        actual.Observaciones = Limpio(cambios.Observaciones);
        actual.IdUsuarioModif = Sesion.IdUsuario;
        actual.FechaModif = DateTime.Now;
        Guardar(() => Datos.GuardarMovimiento(actual));
        Registrar("Movimiento", actual.IdMovimiento, actual.IdSector, "Modificación", $"Causa {actual.NroCausa}: {detalle}");
    }

    // Baja lógica: el movimiento deja de verse pero queda guardado para auditar. No se puede dar de baja un acta, solo sus movimientos.
    public void EliminarMovimiento(int idMovimiento, string motivo)
    {
        var m = Datos.ObtenerMovimiento(idMovimiento) ?? throw new ExcepcionNegocio("El movimiento no existe.");
        Sesion.Exigir(m.IdSector, Accion.Eliminar);
        if (m.Eliminado) return;
        m.Eliminado = true;
        m.IdUsuarioModif = Sesion.IdUsuario;
        m.FechaModif = DateTime.Now;
        Guardar(() => Datos.GuardarMovimiento(m));
        Registrar("Movimiento", m.IdMovimiento, m.IdSector, "Baja", $"Causa {m.NroCausa}: {m.Detalle}" + (Limpio(motivo) is { } x ? $" · Motivo: {x}" : ""));
    }

    public FichaActa ObtenerFicha(int idActa)
    {
        const string ActaNoDisponible = "El acta no existe o no está disponible.";
        var acta = Datos.ObtenerActa(idActa) ?? throw new ExcepcionNegocio(ActaNoDisponible);
        var todos = Datos.ListarMovimientosDeActa(idActa);
        if (!Sesion.EsAdministrador && !todos.Any(m => Sesion.Puede(m.IdSector, Accion.Leer)))
            throw new ExcepcionNegocio(ActaNoDisponible);

        // Recorrido e historial: solo lo de los sectores que la cuenta ve
        var recorrido = todos.Where(m => Sesion.Puede(m.IdSector, Accion.Leer)).ToList();
        var versiones = Datos.ListarVersiones(idActa);
        var historial = Sesion.Filtrar(Datos.ListarHistorial("Acta", idActa)
            .Concat(recorrido.SelectMany(m => Datos.ListarHistorial("Movimiento", m.IdMovimiento))))
            .OrderByDescending(h => h.Fecha).ThenByDescending(h => h.IdHistorial).ToList();

        return new FichaActa
        {
            Acta = acta, Recorrido = recorrido, Versiones = versiones, Historial = historial,
            Persona = acta.IdPersona.HasValue ? Datos.ObtenerPersona(acta.IdPersona.Value) : null,
            Comparacion = Comparar(versiones.FirstOrDefault(), acta),
            PuedeModificar = PuedeModificarActa(acta.IdActa, todos)
        };
    }

    private bool PuedeModificarActa(int idActa, List<Movimiento> movimientos = null)
        => Sesion.EsAdministrador || (movimientos ?? Datos.ListarMovimientosDeActa(idActa)).Any(m => Sesion.Puede(m.IdSector, Accion.Modificar));

    private static List<FilaComparacion> Comparar(ActaVersion original, Acta actual)
    {
        if (original == null) return new();
        FilaComparacion F(string campo, object o, object a) => new() { Campo = campo, Original = Cambios.Formato(o), Actual = Cambios.Formato(a) };
        return
        [
            F("N° de acta", original.NroActa, actual.NroActa),
            F("Fecha del acta", original.FechaActa, actual.FechaActa),
            F("Juzgado", original.Juzgado, actual.Juzgado),
            F("Persona", original.Persona, actual.Persona),
            F("Monto 1", original.Monto1, actual.Monto1),
            F("Monto 2", original.Monto2, actual.Monto2),
            F("Observaciones", original.Observaciones, actual.Observaciones)
        ];
    }

    /// Cambia los datos del acta y guarda la nueva versión. La clave (causa, repartición, año) no se cambia.
   
    public void ModificarActa(Acta cambios, Persona persona, string motivo)
    {
        var actual = Datos.ObtenerActa(cambios.IdActa) ?? throw new ExcepcionNegocio(Sesion.NoDisponible);
        var movimientos = Datos.ListarMovimientosDeActa(actual.IdActa);
        if (!PuedeModificarActa(actual.IdActa, movimientos))
            throw new ExcepcionNegocio(Sesion.EsAdministrador || movimientos.Any(m => Sesion.Puede(m.IdSector, Accion.Leer))
                ? Sesion.AccionNoDisponible : Sesion.NoDisponible);
        Requerido(motivo, "el motivo del cambio");
        Largo(motivo, 200, "El motivo");
        ValidarActa(cambios);
        ValidarPersona(persona);

        int? idPersona = null;
        Guardar(() => idPersona = ResolverPersona(persona, actual.IdPersona));
        string juzgadoNuevo = Datos.ListarJuzgados().FirstOrDefault(j => j.IdJuzgado == cambios.IdJuzgado)?.Nombre;
        string personaNueva = idPersona == null ? null : Datos.ObtenerPersona(idPersona.Value)?.NombreCompleto;
        var detalle = new Cambios()
            .Campo("N° de acta", actual.NroActa, cambios.NroActa.Trim()).Campo("Fecha del acta", actual.FechaActa, cambios.FechaActa.Date)
            .Campo("Juzgado", actual.Juzgado, juzgadoNuevo).Campo("Persona", actual.Persona, personaNueva)
            .Campo("Monto 1", actual.Monto1, cambios.Monto1).Campo("Monto 2", actual.Monto2, cambios.Monto2)
            .Campo("Observaciones", actual.Observaciones, Limpio(cambios.Observaciones));
        if (!detalle.Hay) throw new ExcepcionNegocio("No hay cambios para guardar.");

        actual.NroActa = cambios.NroActa.Trim();
        actual.FechaActa = cambios.FechaActa.Date;
        actual.IdJuzgado = cambios.IdJuzgado;
        actual.IdPersona = idPersona;
        actual.Monto1 = cambios.Monto1;
        actual.Monto2 = cambios.Monto2;
        actual.Observaciones = Limpio(cambios.Observaciones);
        actual.Version++;
        actual.IdUsuarioModif = Sesion.IdUsuario;
        actual.FechaModif = DateTime.Now;
        Guardar(() =>
        {
            Datos.GuardarActa(actual);
            Datos.AgregarVersion(ActaVersion.Desde(actual, actual.Version, motivo.Trim(), Sesion.IdUsuario, actual.FechaModif.Value));
        });
        int? sector = movimientos.Select(m => m.IdSector).FirstOrDefault(s => Sesion.Puede(s, Accion.Modificar));
        Registrar("Acta", actual.IdActa, sector == 0 ? null : sector, "Modificación",
            $"Versión {actual.Version} · {motivo.Trim()} · {detalle}");
    }

    // Devuelve el Id de la persona: la busca por documento, reusa la del acta si no cambió, o la da de alta.
    private int? ResolverPersona(Persona p, int? idActual = null)
    {
        if (p == null || string.IsNullOrWhiteSpace(p.Apellido)) return null;
        p.Apellido = p.Apellido.Trim();
        p.Nombre = Limpio(p.Nombre);
        p.NroDocumento = Limpio(p.NroDocumento);
        var actual = idActual == null ? null : Datos.ObtenerPersona(idActual.Value);
        if (actual != null && actual.Apellido == p.Apellido && actual.Nombre == p.Nombre
            && actual.TipoDocumento == p.TipoDocumento && actual.NroDocumento == p.NroDocumento)
            return actual.IdPersona;
        if (p.NroDocumento != null)
        {
            var existente = Datos.BuscarPersonaPorDocumento(p.TipoDocumento, p.NroDocumento);
            if (existente != null)
            {
                if (existente.Apellido != p.Apellido || existente.Nombre != p.Nombre)
                {
                    existente.Apellido = p.Apellido;
                    existente.Nombre = p.Nombre;
                    Datos.GuardarPersona(existente);
                }
                return existente.IdPersona;
            }
        }
        p.IdPersona = 0;
        Datos.GuardarPersona(p);
        return p.IdPersona;
    }

    private static void ValidarClave(Acta a)
    {
        Requerido(a.NroCausa, "el N° de causa");
        Largo(a.NroCausa.Trim(), 20, "El N° de causa");
        a.NroCausa = a.NroCausa.Trim();
        if (a.IdReparticion == 0) throw new ExcepcionNegocio("Falta elegir la repartición.");
        if (a.Anio < 1990 || a.Anio > DateTime.Today.Year + 1) throw new ExcepcionNegocio("El año del acta no es válido.");
    }

    private static void ValidarActa(Acta a)
    {
        Requerido(a.NroActa, "el N° de acta");
        Largo(a.NroActa.Trim(), 20, "El N° de acta");
        Largo(a.Observaciones, 500, "Las observaciones del acta");
        if (a.FechaActa.Date > DateTime.Today) throw new ExcepcionNegocio("La fecha del acta no puede ser futura.");
        if (a.Monto1 < 0 || a.Monto2 < 0) throw new ExcepcionNegocio("Los montos no pueden ser negativos.");
    }

    private static void ValidarPersona(Persona p)
    {
        if (p == null || string.IsNullOrWhiteSpace(p.Apellido)) return;
        Largo(p.Apellido.Trim(), 80, "El apellido");
        Largo(p.Nombre?.Trim(), 80, "El nombre");
        var doc = Limpio(p.NroDocumento);
        if (doc == null) return;
        if (!doc.All(char.IsDigit)) throw new ExcepcionNegocio("El documento va solo con números, sin puntos ni guiones.");
        if (p.TipoDocumento == "DNI" && (doc.Length < 7 || doc.Length > 8)) throw new ExcepcionNegocio("El DNI tiene que tener 7 u 8 números.");
        if (p.TipoDocumento == "CUIT" && doc.Length != 11) throw new ExcepcionNegocio("El CUIT tiene que tener 11 números.");
    }

    private static void ValidarMovimiento(Movimiento m)
    {
        Requerido(m.Detalle, "el movimiento");
        Largo(m.Detalle.Trim(), 100, "El movimiento");
        Largo(Limpio(m.Remito), 30, "El remito");
        Largo(m.Observaciones, 500, "Las observaciones");
        if (m.Fecha.Date > DateTime.Today) throw new ExcepcionNegocio("La fecha del movimiento no puede ser futura.");
        m.Detalle = m.Detalle.Trim();
    }

    // reclamos

    public Reclamo ObtenerReclamo(int idSector, int idReclamo)
    {
        Sesion.Exigir(idSector, Accion.Leer);
        return Datos.ObtenerReclamo(idReclamo) ?? throw new ExcepcionNegocio("El reclamo no existe.");
    }

    public void CargarReclamo(int idSector, Reclamo r)
    {
        Sesion.Exigir(idSector, Accion.Cargar);
        ValidarReclamo(r);
        r.IdReclamo = 0;
        r.Eliminado = false;
        r.IdUsuarioAlta = Sesion.IdUsuario;
        r.FechaAlta = DateTime.Now;
        Guardar(() => Datos.GuardarReclamo(r));
        Registrar("Reclamo", r.IdReclamo, idSector, "Alta", $"Caso {r.NroCaso} · {r.Ubicacion}");
    }

    public void ModificarReclamo(int idSector, Reclamo cambios)
    {
        Sesion.Exigir(idSector, Accion.Modificar);
        var actual = Datos.ObtenerReclamo(cambios.IdReclamo) ?? throw new ExcepcionNegocio("El reclamo no existe.");
        ValidarReclamo(cambios);
        var detalle = new Cambios()
            .Campo("N° de caso", actual.NroCaso, cambios.NroCaso).Campo("Fecha", actual.Fecha, cambios.Fecha)
            .Campo("Ubicación", actual.Ubicacion, cambios.Ubicacion).Campo("Vehículo", actual.Vehiculo, cambios.Vehiculo)
            .Campo("Verificado", actual.Verificado, cambios.Verificado).Campo("Fecha de verificación", actual.FechaVerificacion, cambios.FechaVerificacion)
            .Campo("Verificado por", actual.VerificadoPor, cambios.VerificadoPor).Campo("Observaciones", actual.Observaciones, cambios.Observaciones);
        if (!detalle.Hay) return;
        cambios.Eliminado = actual.Eliminado;
        cambios.IdUsuarioAlta = actual.IdUsuarioAlta;
        cambios.FechaAlta = actual.FechaAlta;
        cambios.IdUsuarioModif = Sesion.IdUsuario;
        cambios.FechaModif = DateTime.Now;
        Guardar(() => Datos.GuardarReclamo(cambios));
        Registrar("Reclamo", cambios.IdReclamo, idSector, "Modificación", $"Caso {cambios.NroCaso}: {detalle}");
    }

    public void EliminarReclamo(int idSector, int idReclamo, string motivo)
    {
        Sesion.Exigir(idSector, Accion.Eliminar);
        var r = Datos.ObtenerReclamo(idReclamo) ?? throw new ExcepcionNegocio("El reclamo no existe.");
        r.Eliminado = true;
        r.IdUsuarioModif = Sesion.IdUsuario;
        r.FechaModif = DateTime.Now;
        Guardar(() => Datos.GuardarReclamo(r));
        Registrar("Reclamo", r.IdReclamo, idSector, "Baja", $"Caso {r.NroCaso}" + (Limpio(motivo) is { } x ? $" · Motivo: {x}" : ""));
    }

    private static void ValidarReclamo(Reclamo r)
    {
        Requerido(r.NroCaso, "el N° de caso");
        Requerido(r.Ubicacion, "la ubicación");
        r.NroCaso = r.NroCaso.Trim();
        r.Ubicacion = r.Ubicacion.Trim();
        r.Vehiculo = Limpio(r.Vehiculo);
        r.VerificadoPor = Limpio(r.VerificadoPor);
        r.Observaciones = Limpio(r.Observaciones);
        Largo(r.NroCaso, 20, "El N° de caso");
        Largo(r.Ubicacion, 150, "La ubicación");
        Largo(r.Vehiculo, 100, "El vehículo");
        Largo(r.VerificadoPor, 80, "Verificado por");
        Largo(r.Observaciones, 500, "Las observaciones");
        if (r.Fecha.Date > DateTime.Today) throw new ExcepcionNegocio("La fecha del reclamo no puede ser futura.");
        if (r.Verificado)
        {
            if (r.FechaVerificacion == null) throw new ExcepcionNegocio("Falta la fecha en que se fue a verificar.");
            if (r.FechaVerificacion.Value.Date < r.Fecha.Date) throw new ExcepcionNegocio("La verificación no puede ser anterior al reclamo.");
            Requerido(r.VerificadoPor, "quién fue a verificar");
        }
        else
        {
            r.FechaVerificacion = null;
            r.VerificadoPor = null;
        }
    }

    // lotes

    public Lote ObtenerLote(int idSector, int idLote)
    {
        Sesion.Exigir(idSector, Accion.Leer);
        return Datos.ObtenerLote(idLote) ?? throw new ExcepcionNegocio("El lote no existe.");
    }

    public void CargarLote(int idSector, Lote l)
    {
        Sesion.Exigir(idSector, Accion.Cargar);
        ValidarLote(l);
        l.IdLote = 0;
        l.Eliminado = false;
        l.IdUsuarioAlta = Sesion.IdUsuario;
        l.FechaAlta = DateTime.Now;
        Guardar(() => Datos.GuardarLote(l));
        Registrar("Lote", l.IdLote, idSector, "Alta", $"Lote {l.NroLote} · {l.Total} actas");
    }

    public void ModificarLote(int idSector, Lote cambios)
    {
        Sesion.Exigir(idSector, Accion.Modificar);
        var actual = Datos.ObtenerLote(cambios.IdLote) ?? throw new ExcepcionNegocio("El lote no existe.");
        ValidarLote(cambios);
        var detalle = new Cambios()
            .Campo("N° de lote", actual.NroLote, cambios.NroLote).Campo("Fecha", actual.Fecha, cambios.Fecha)
            .Campo("Tránsito", actual.CantTransito, cambios.CantTransito).Campo("Competencia", actual.CantCompetencia, cambios.CantCompetencia)
            .Campo("Prescriptas", actual.CantPrescriptas, cambios.CantPrescriptas).Campo("Observaciones", actual.Observaciones, cambios.Observaciones);
        if (!detalle.Hay) return;
        cambios.Eliminado = actual.Eliminado;
        cambios.IdUsuarioAlta = actual.IdUsuarioAlta;
        cambios.FechaAlta = actual.FechaAlta;
        cambios.IdUsuarioModif = Sesion.IdUsuario;
        cambios.FechaModif = DateTime.Now;
        Guardar(() => Datos.GuardarLote(cambios));
        Registrar("Lote", cambios.IdLote, idSector, "Modificación", $"Lote {cambios.NroLote}: {detalle}");
    }

    public void EliminarLote(int idSector, int idLote, string motivo)
    {
        Sesion.Exigir(idSector, Accion.Eliminar);
        var l = Datos.ObtenerLote(idLote) ?? throw new ExcepcionNegocio("El lote no existe.");
        l.Eliminado = true;
        l.IdUsuarioModif = Sesion.IdUsuario;
        l.FechaModif = DateTime.Now;
        Guardar(() => Datos.GuardarLote(l));
        Registrar("Lote", l.IdLote, idSector, "Baja", $"Lote {l.NroLote}" + (Limpio(motivo) is { } x ? $" · Motivo: {x}" : ""));
    }

    private static void ValidarLote(Lote l)
    {
        Requerido(l.NroLote, "el N° de lote");
        l.NroLote = l.NroLote.Trim();
        l.Observaciones = Limpio(l.Observaciones);
        Largo(l.NroLote, 20, "El N° de lote");
        Largo(l.Observaciones, 500, "Las observaciones");
        if (l.CantTransito < 0 || l.CantCompetencia < 0 || l.CantPrescriptas < 0) throw new ExcepcionNegocio("Las cantidades no pueden ser negativas.");
        if (l.Total == 0) throw new ExcepcionNegocio("El lote no tiene actas.");
        if (l.Fecha.Date > DateTime.Today) throw new ExcepcionNegocio("La fecha del lote no puede ser futura.");
    }
}
