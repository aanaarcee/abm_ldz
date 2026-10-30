using System.Security.Cryptography;
using Oficina.Datos;
using Oficina.Entidades;

namespace Oficina.Negocio;

// mensajes de errores (validación, permisos, datos repetidos)
public class ExcepcionNegocio : Exception
{
    public ExcepcionNegocio(string mensaje, Exception interna = null) : base(mensaje, interna) { }
}

//m guardado de hash y sal de contraseñas, verificación y validación de longitud
public static class Contrasenas
{
    private const int Iteraciones = 100_000;
    private const int LargoHash = 32;   // VARBINARY(32)
    private const int LargoSal = 16;    // VARBINARY(16)

    public static (byte[] Hash, byte[] Sal) Generar(string clave)
    {
        var sal = RandomNumberGenerator.GetBytes(LargoSal);
        return (Calcular(clave, sal), sal);
    }

    public static bool Verificar(string clave, byte[] hash, byte[] sal)
        => hash != null && sal != null && CryptographicOperations.FixedTimeEquals(Calcular(clave, sal), hash);

    private static byte[] Calcular(string clave, byte[] sal)
        => Rfc2898DeriveBytes.Pbkdf2(clave ?? "", sal, Iteraciones, HashAlgorithmName.SHA256, LargoHash);

    public static void Validar(string clave)
    {
        if (string.IsNullOrWhiteSpace(clave) || clave.Length < 6)
            throw new ExcepcionNegocio("La contraseña tiene que tener al menos 6 caracteres.");
    }
}

// permisos de cada sector
public class Sesion
{
    private readonly Dictionary<int, Accion> _permisos;

    // errores sobre los permisos
    public const string NoDisponible = "Ese registro no existe o no está disponible.";
    public const string AccionNoDisponible = "Esa acción no está disponible.";

    private readonly HashSet<int> _cuentasVisibles;   // null: todas (Administrador)

    public Usuario Usuario { get; }
    public Rol Rol { get; }
    // sectores que puede ver (no necesariamente todos los de su rol)
    public IReadOnlyList<Sector> Sectores { get; }

    public Sesion(Usuario usuario, Rol rol, IEnumerable<Sector> sectores, IEnumerable<Permiso> permisos, IEnumerable<int> cuentasVisibles = null)
    {
        Usuario = usuario;
        Rol = rol;
        _permisos = permisos.ToDictionary(p => p.IdSector, p => p.Acciones);
        Sectores = sectores.Where(s => Puede(s.IdSector, Accion.Leer)).ToList();
        _cuentasVisibles = rol.EsAdministrador || cuentasVisibles == null ? null : cuentasVisibles.Append(usuario.IdUsuario).ToHashSet();
    }

    public bool EsAdministrador => Rol.EsAdministrador;
    public int IdUsuario => Usuario.IdUsuario;

    public bool Puede(int idSector, Accion accion)
        => EsAdministrador || (_permisos.TryGetValue(idSector, out var a) && (a & accion) == accion);

    public IEnumerable<Sector> SectoresVisibles => Sectores;
    public IEnumerable<int> IdsVisibles => Sectores.Select(s => s.IdSector);

    public Sector Sector(int idSector) => Sectores.FirstOrDefault(s => s.IdSector == idSector)
                                          ?? throw new ExcepcionNegocio(NoDisponible);

    // si no puede ver el sector, el mensaje es el mismo que si no existiera
    public void Exigir(int idSector, Accion accion)
    {
        if (Puede(idSector, accion)) return;
        throw new ExcepcionNegocio(Puede(idSector, Accion.Leer) ? AccionNoDisponible : NoDisponible);
    }

    public void ExigirAdministrador()
    {
        if (!EsAdministrador) throw new ExcepcionNegocio(AccionNoDisponible);
    }

   
    // Deja solo las filas de historial de sectores que la cuenta ve, y oculta el nombre de las cuentas
    public List<EntradaHistorial> Filtrar(IEnumerable<EntradaHistorial> historial)
    {
        if (EsAdministrador) return historial.ToList();
        var lista = historial.Where(h => h.IdSector is int id && Puede(id, Accion.Leer)).ToList();
        foreach (var h in lista.Where(h => _cuentasVisibles != null && !_cuentasVisibles.Contains(h.IdUsuario)))
            h.Usuario = "—";
        return lista;
    }

    // Entra con usuario y contraseña. Devuelve la sesión o lanza ExcepcionNegocio 
    public static Sesion Ingresar(IDatosSeguridad datos, string nombreUsuario, string clave)
    {
        var u = string.IsNullOrWhiteSpace(nombreUsuario) ? null : datos.ObtenerUsuario(nombreUsuario.Trim());
        if (u == null || !Contrasenas.Verificar(clave, u.ClaveHash, u.ClaveSal))
            throw new ExcepcionNegocio("Usuario o contraseña incorrectos.");
        if (!u.Activo)
            throw new ExcepcionNegocio("La cuenta está desactivada. Hable con el Administrador.");
        return Armar(datos, u);
    }

    // Vuelve a leer sectores y permisos de la misma cuenta (por ejemplo, después de cambiarlos en Configuración)
    public static Sesion Recargar(IDatosSeguridad datos, Sesion actual)
        => Armar(datos, datos.ObtenerUsuario(actual.Usuario.NombreUsuario) ?? actual.Usuario);

    private static Sesion Armar(IDatosSeguridad datos, Usuario u)
    {
        var rol = datos.ObtenerRol(u.IdRol);
        var permisos = datos.ListarPermisos(rol.IdRol);
        // Cuentas cuyo nombre puede ver: las de roles cuyo sector propio también ve
        var legibles = permisos.Where(p => p.Puede(Accion.Leer)).Select(p => p.IdSector).ToHashSet();
        var roles = datos.ListarRoles().Where(r => !r.EsAdministrador && r.IdSectorPropio is int s && legibles.Contains(s)).Select(r => r.IdRol).ToHashSet();
        var cuentas = rol.EsAdministrador ? null : datos.ListarUsuarios().Where(x => roles.Contains(x.IdRol)).Select(x => x.IdUsuario);
        return new Sesion(u, rol, datos.ListarSectores(), permisos, cuentas);
    }
}
