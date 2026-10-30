namespace Oficina.Entidades;

// filas para cada sector
public enum TipoSector
{
    Consulta = 0,   // no carga datos propios (por ejemplo, Estadísticas)
    Actas = 1,      // anota movimientos de actas
    Reclamos = 2,
    Lotes = 3
}

[Flags]
public enum Accion
{
    Ninguna = 0,
    Leer = 1,
    Cargar = 2,
    Modificar = 4,
    Eliminar = 8,
    Todas = Leer | Cargar | Modificar | Eliminar
}

public class Sector
{
    public int IdSector { get; set; }
    public string Nombre { get; set; }
    public string Clave { get; set; }
    public TipoSector Tipo { get; set; }

    public override string ToString() => Nombre;
}

/// cada rol seria un departameento de la oficina
public class Rol
{
    public int IdRol { get; set; }
    public string Nombre { get; set; }
    public int? IdSectorPropio { get; set; }
    public bool EsAdministrador { get; set; }

    public override string ToString() => Nombre;
}

// cuenta de un sector
public class Usuario
{
    public int IdUsuario { get; set; }
    public string NombreUsuario { get; set; }
    public byte[] ClaveHash { get; set; }
    public byte[] ClaveSal { get; set; }
    public int IdRol { get; set; }
    public bool Activo { get; set; } = true;
}

public class Permiso
{
    public int IdRol { get; set; }
    public int IdSector { get; set; }
    public Accion Acciones { get; set; }

    public bool Puede(Accion accion) => (Acciones & accion) == accion;
}
