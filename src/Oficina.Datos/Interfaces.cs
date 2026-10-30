using Oficina.Entidades;

namespace Oficina.Datos;

// sectores roles usuarios y permisos
public interface IDatosSeguridad
{
    bool HayUsuarios();

    List<Sector> ListarSectores();
   // da de alta el sector si es  0
    void GuardarSector(Sector sector);

    List<Rol> ListarRoles();
    Rol ObtenerRol(int idRol);
    void GuardarRol(Rol rol);

    List<Usuario> ListarUsuarios();
    Usuario ObtenerUsuario(string nombreUsuario);
    void GuardarUsuario(Usuario usuario);

    List<Permiso> ListarPermisos(int idRol);
    // reemplaza los datos de los sectores y acciones que puede hacer el rol. Si no tiene ninguno, se borran todos.
    void GuardarPermisos(int idRol, IEnumerable<Permiso> permisos);
}

// actas movimientos y sectores
public interface IDatosRegistros
{
    List<Reparticion> ListarReparticiones();
    void GuardarReparticion(Reparticion reparticion);
    List<Juzgado> ListarJuzgados();
    void GuardarJuzgado(Juzgado juzgado);

    Persona ObtenerPersona(int idPersona);
    Persona BuscarPersonaPorDocumento(string tipoDocumento, string nroDocumento);
    void GuardarPersona(Persona persona);

    Acta ObtenerActa(int idActa);
    // busqueda
    Acta BuscarActa(string nroCausa, int idReparticion, int anio);
    void GuardarActa(Acta acta);
    List<ActaVersion> ListarVersiones(int idActa);
    void AgregarVersion(ActaVersion version);

    Movimiento ObtenerMovimiento(int idMovimiento);
    // movimeintos de un sector, o de un acta
    List<Movimiento> ListarMovimientos(int idSector);
    List<Movimiento> ListarMovimientosDeActa(int idActa);
    void GuardarMovimiento(Movimiento movimiento);
    Dictionary<int, int> ContarMovimientosPorSector();

    Reclamo ObtenerReclamo(int idReclamo);
    List<Reclamo> ListarReclamos();
    void GuardarReclamo(Reclamo reclamo);

    Lote ObtenerLote(int idLote);
    List<Lote> ListarLotes();
    void GuardarLote(Lote lote);

    void AgregarHistorial(EntradaHistorial entrada);
    List<EntradaHistorial> ListarHistorial(string entidad, int idEntidad);
    List<EntradaHistorial> ListarUltimosCambios(IEnumerable<int> sectores, int cantidad);

    // busqueda general del sector, o de un sector en particular. Devuelve actas, reclamos y lotes
    List<ResultadoBusqueda> Buscar(string texto, IEnumerable<int> sectores, int? idSector = null);
}
