namespace Oficina.Entidades;

public class Reparticion
{
    public int IdReparticion { get; set; }
    public string Nombre { get; set; }
    public override string ToString() => Nombre;
}

public class Juzgado
{
    public int IdJuzgado { get; set; }
    public string Nombre { get; set; }
    public override string ToString() => Nombre;
}

public class Persona
{
    public int IdPersona { get; set; }
    public string Apellido { get; set; }
    public string Nombre { get; set; }
    public string TipoDocumento { get; set; } = "DNI";
    public string NroDocumento { get; set; }

    public string NombreCompleto => string.IsNullOrWhiteSpace(Nombre) ? Apellido : $"{Apellido}, {Nombre}";
    public override string ToString() => NombreCompleto;
}

// campos comunes a actas, movimientos, reclamos y lotes
public abstract class RegistroAuditado
{
    public bool Eliminado { get; set; }
    public int IdUsuarioAlta { get; set; }
    public DateTime FechaAlta { get; set; }
    public int? IdUsuarioModif { get; set; }
    public DateTime? FechaModif { get; set; }

    public DateTime UltimaModificacion => FechaModif ?? FechaAlta;
}


// registro de act y como se define

public class Acta : RegistroAuditado
{
    public int IdActa { get; set; }
    public string NroCausa { get; set; }
    public int IdReparticion { get; set; }
    public int Anio { get; set; }
    public string NroActa { get; set; }
    public int? IdJuzgado { get; set; }
    public int? IdPersona { get; set; }
    public DateTime FechaActa { get; set; }
    public decimal? Monto1 { get; set; }
    public decimal? Monto2 { get; set; }
    public string Observaciones { get; set; }
    public int Version { get; set; } = 1;

    // Datos para mostrar, completados por la capa de datos
    public string Reparticion { get; set; }
    public string Juzgado { get; set; }
    public string Persona { get; set; }
    public string Documento { get; set; }

    public string Clave => $"Causa {NroCausa} · {Reparticion} · {Anio}";

    public Acta Copiar() => (Acta)MemberwiseClone();
}

// version guardada del acta, para ver cambios y auditar
public class ActaVersion
{
    public int IdActaVersion { get; set; }
    public int IdActa { get; set; }
    public int NroVersion { get; set; }
    public string NroActa { get; set; }
    public int? IdJuzgado { get; set; }
    public int? IdPersona { get; set; }
    public DateTime FechaActa { get; set; }
    public decimal? Monto1 { get; set; }
    public decimal? Monto2 { get; set; }
    public string Observaciones { get; set; }
    public string Motivo { get; set; }
    public int IdUsuario { get; set; }
    public DateTime Fecha { get; set; }

    public string Juzgado { get; set; }
    public string Persona { get; set; }

    public static ActaVersion Desde(Acta a, int nroVersion, string motivo, int idUsuario, DateTime fecha) => new()
    {
        IdActa = a.IdActa, NroVersion = nroVersion, NroActa = a.NroActa, IdJuzgado = a.IdJuzgado, IdPersona = a.IdPersona,
        FechaActa = a.FechaActa, Monto1 = a.Monto1, Monto2 = a.Monto2, Observaciones = a.Observaciones,
        Motivo = motivo, IdUsuario = idUsuario, Fecha = fecha
    };
}

// lo que un sector anota de cada acta (forma de auditar los cambios)
public class Movimiento : RegistroAuditado
{
    public int IdMovimiento { get; set; }
    public int IdActa { get; set; }
    public int IdSector { get; set; }
    public DateTime Fecha { get; set; }
    public string Detalle { get; set; }
    public string Remito { get; set; }
    public string Observaciones { get; set; }

    // Datos del acta para mostrar en la grilla del sector
    public string Sector { get; set; }
    public string NroCausa { get; set; }
    public string Reparticion { get; set; }
    public int Anio { get; set; }
    public string NroActa { get; set; }
    public string Persona { get; set; }
    public string Documento { get; set; }
    public decimal? Monto1 { get; set; }
    public decimal? Monto2 { get; set; }
}

public class Lote : RegistroAuditado
{
    public int IdLote { get; set; }
    public string NroLote { get; set; }
    public DateTime Fecha { get; set; }
    public int CantTransito { get; set; }
    public int CantCompetencia { get; set; }
    public int CantPrescriptas { get; set; }
    public string Observaciones { get; set; }

    public int Total => CantTransito + CantCompetencia + CantPrescriptas;
}

// avisos de los reclamos de los vecinos, que no son actas ni movimientos
public class Reclamo : RegistroAuditado
{
    public int IdReclamo { get; set; }
    public string NroCaso { get; set; }
    public DateTime Fecha { get; set; }
    public string Ubicacion { get; set; }
    public string Vehiculo { get; set; }
    public bool Verificado { get; set; }
    public DateTime? FechaVerificacion { get; set; }
    public string VerificadoPor { get; set; }
    public string Observaciones { get; set; }
}

public class EntradaHistorial
{
    public long IdHistorial { get; set; }
    public string Entidad { get; set; }
    public int IdEntidad { get; set; }
    public int? IdSector { get; set; }
    public string Accion { get; set; }
    public string Detalle { get; set; }
    public int IdUsuario { get; set; }
    public DateTime Fecha { get; set; }

    public string Usuario { get; set; }
    public string Sector { get; set; }
}

// fila de la busqueda general
public class ResultadoBusqueda
{
    public string Tipo { get; set; }          // Acta, Reclamo, Lote
    public int IdSector { get; set; }
    public string Sector { get; set; }
    public int Id { get; set; }               // IdMovimiento, IdReclamo o IdLote
    public int? IdActa { get; set; }
    public string Titulo { get; set; }
    public string Detalle { get; set; }
    public DateTime Fecha { get; set; }
}
