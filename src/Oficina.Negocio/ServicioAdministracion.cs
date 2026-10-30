using Oficina.Datos;
using Oficina.Entidades;

namespace Oficina.Negocio;

//Una fila de la tabla de permisos: un sector con lo que el rol puede hacer ahí
public class FilaPermiso
{
    public Sector Sector { get; set; }
    public bool Leer { get; set; }
    public bool Cargar { get; set; }
    public bool Modificar { get; set; }
    public bool Eliminar { get; set; }

    public Accion Acciones => (Leer ? Accion.Leer : 0) | (Cargar ? Accion.Cargar : 0) | (Modificar ? Accion.Modificar : 0) | (Eliminar ? Accion.Eliminar : 0);
}

// Roles, permisos, cuentas, sectores y listas. Solo para el Administrador
public class ServicioAdministracion : ServicioBase
{
    private readonly IDatosSeguridad _seguridad;

    public ServicioAdministracion(Sesion sesion, IDatosSeguridad seguridad, IDatosRegistros datos) : base(sesion, datos)
    {
        sesion.ExigirAdministrador();
        _seguridad = seguridad;
    }

    // sectores

    public List<Sector> Sectores() => _seguridad.ListarSectores();

    public void GuardarSector(Sector s)
    {
        Requerido(s.Nombre, "el nombre del sector");
        Requerido(s.Clave, "la clave del sector");
        s.Nombre = s.Nombre.Trim();
        s.Clave = s.Clave.Trim().ToUpperInvariant();
        Largo(s.Nombre, 60, "El nombre");
        Largo(s.Clave, 4, "La clave");
        bool alta = s.IdSector == 0;
        Guardar(() => _seguridad.GuardarSector(s));
        Registrar("Sector", s.IdSector, null, alta ? "Alta" : "Modificación", $"{s.Nombre} ({s.Clave}) · {s.Tipo}");
    }

    // roles y permisos

    public List<Rol> Roles() => _seguridad.ListarRoles();

    public void GuardarRol(Rol r)
    {
        Requerido(r.Nombre, "el nombre del rol");
        r.Nombre = r.Nombre.Trim();
        Largo(r.Nombre, 60, "El nombre");
        if (r.IdRol == Sesion.Rol.IdRol && !r.EsAdministrador)
            throw new ExcepcionNegocio("No puede quitarle el permiso de Administrador a su propio rol.");
        // El Administrador es una cuenta aparte: la cuenta general de un sector nunca puede serlo
        if (r.EsAdministrador && r.IdSectorPropio != null)
            throw new ExcepcionNegocio("El rol de un sector no puede ser Administrador. El Administrador es una cuenta aparte.");
        bool alta = r.IdRol == 0;
        Guardar(() => _seguridad.GuardarRol(r));
        Registrar("Rol", r.IdRol, null, alta ? "Alta" : "Modificación", r.Nombre + (r.EsAdministrador ? " · Administrador" : ""));
    }

    public List<FilaPermiso> Permisos(int idRol)
    {
        var actuales = _seguridad.ListarPermisos(idRol).ToDictionary(p => p.IdSector, p => p.Acciones);
        return _seguridad.ListarSectores().Select(s =>
        {
            var a = actuales.GetValueOrDefault(s.IdSector);
            return new FilaPermiso
            {
                Sector = s, Leer = a.HasFlag(Accion.Leer), Cargar = a.HasFlag(Accion.Cargar),
                Modificar = a.HasFlag(Accion.Modificar), Eliminar = a.HasFlag(Accion.Eliminar)
            };
        }).ToList();
    }

    // Guarda la tabla de permisos. Cargar, modificar o eliminar implican poder leer
    public void GuardarPermisos(int idRol, IEnumerable<FilaPermiso> filas)
    {
        var rol = _seguridad.ObtenerRol(idRol) ?? throw new ExcepcionNegocio("El rol no existe.");
        var lista = filas.Select(f => new Permiso
        {
            IdRol = idRol, IdSector = f.Sector.IdSector,
            Acciones = f.Acciones == Accion.Ninguna ? Accion.Ninguna : f.Acciones | Accion.Leer
        }).ToList();
        _seguridad.GuardarPermisos(idRol, lista);
        var resumen = string.Join(", ", lista.Where(p => p.Acciones != Accion.Ninguna)
            .Select(p => $"{filas.First(f => f.Sector.IdSector == p.IdSector).Sector.Clave}:{Letras(p.Acciones)}"));
        Registrar("Permiso", idRol, null, "Modificación", $"{rol.Nombre} · {(resumen == "" ? "sin permisos" : resumen)}");
    }

    private static string Letras(Accion a) =>
        (a.HasFlag(Accion.Leer) ? "L" : "") + (a.HasFlag(Accion.Cargar) ? "C" : "") + (a.HasFlag(Accion.Modificar) ? "M" : "") + (a.HasFlag(Accion.Eliminar) ? "E" : "");

    // cuentas

    public List<Usuario> Usuarios() => _seguridad.ListarUsuarios();

    public void CrearUsuario(string nombreUsuario, string clave, int idRol)
    {
        Requerido(nombreUsuario, "el nombre de la cuenta");
        nombreUsuario = nombreUsuario.Trim().ToLowerInvariant();
        Largo(nombreUsuario, 30, "El nombre de la cuenta");
        if (nombreUsuario.Any(char.IsWhiteSpace)) throw new ExcepcionNegocio("El nombre de la cuenta no puede tener espacios.");
        Contrasenas.Validar(clave);
        if (_seguridad.ObtenerRol(idRol) == null) throw new ExcepcionNegocio("Falta elegir el rol.");
        var (hash, sal) = Contrasenas.Generar(clave);
        var u = new Usuario { NombreUsuario = nombreUsuario, ClaveHash = hash, ClaveSal = sal, IdRol = idRol, Activo = true };
        Guardar(() => _seguridad.GuardarUsuario(u));
        Registrar("Usuario", u.IdUsuario, null, "Alta", nombreUsuario);
    }

    public void CambiarClave(int idUsuario, string clave)
    {
        Contrasenas.Validar(clave);
        var u = ObtenerUsuario(idUsuario);
        (u.ClaveHash, u.ClaveSal) = Contrasenas.Generar(clave);
        Guardar(() => _seguridad.GuardarUsuario(u));
        Registrar("Usuario", u.IdUsuario, null, "Modificación", $"{u.NombreUsuario} · cambio de contraseña");
    }

    public void CambiarRolYEstado(int idUsuario, int idRol, bool activo)
    {
        var u = ObtenerUsuario(idUsuario);
        if (u.IdUsuario == Sesion.IdUsuario && (!activo || idRol != u.IdRol))
            throw new ExcepcionNegocio("No puede desactivar ni cambiarle el rol a su propia cuenta.");
        var detalle = new Cambios().Campo("Rol", _seguridad.ObtenerRol(u.IdRol)?.Nombre, _seguridad.ObtenerRol(idRol)?.Nombre).Campo("Activa", u.Activo, activo);
        if (!detalle.Hay) return;
        u.IdRol = idRol;
        u.Activo = activo;
        Guardar(() => _seguridad.GuardarUsuario(u));
        Registrar("Usuario", u.IdUsuario, null, "Modificación", $"{u.NombreUsuario} · {detalle}");
    }

    private Usuario ObtenerUsuario(int idUsuario)
        => _seguridad.ListarUsuarios().FirstOrDefault(x => x.IdUsuario == idUsuario) ?? throw new ExcepcionNegocio("La cuenta no existe.");

    // listas y categorias

    public List<Reparticion> Reparticiones() => Datos.ListarReparticiones();
    public List<Juzgado> Juzgados() => Datos.ListarJuzgados();

    public void GuardarReparticion(Reparticion r)
    {
        Requerido(r.Nombre, "el nombre de la repartición");
        r.Nombre = r.Nombre.Trim();
        Largo(r.Nombre, 80, "El nombre");
        bool alta = r.IdReparticion == 0;
        Guardar(() => Datos.GuardarReparticion(r));
        Registrar("Reparticion", r.IdReparticion, null, alta ? "Alta" : "Modificación", r.Nombre);
    }

    public void GuardarJuzgado(Juzgado j)
    {
        Requerido(j.Nombre, "el nombre del juzgado");
        j.Nombre = j.Nombre.Trim();
        Largo(j.Nombre, 80, "El nombre");
        bool alta = j.IdJuzgado == 0;
        Guardar(() => Datos.GuardarJuzgado(j));
        Registrar("Juzgado", j.IdJuzgado, null, alta ? "Alta" : "Modificación", j.Nombre);
    }

    // cambios de administrador
    public List<EntradaHistorial> HistorialAdministracion(int cantidad = 50)
        => new[] { "Sector", "Rol", "Permiso", "Usuario", "Reparticion", "Juzgado" }
            .SelectMany(e => IdsDe(e).SelectMany(id => Datos.ListarHistorial(e, id)))
            .OrderByDescending(h => h.Fecha).ThenByDescending(h => h.IdHistorial).Take(cantidad).ToList();

    private IEnumerable<int> IdsDe(string entidad) => entidad switch
    {
        "Sector" => _seguridad.ListarSectores().Select(x => x.IdSector),
        "Rol" or "Permiso" => _seguridad.ListarRoles().Select(x => x.IdRol),
        "Usuario" => _seguridad.ListarUsuarios().Select(x => x.IdUsuario),
        "Reparticion" => Datos.ListarReparticiones().Select(x => x.IdReparticion),
        _ => Datos.ListarJuzgados().Select(x => x.IdJuzgado)
    };
}
