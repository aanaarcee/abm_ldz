using Oficina.Datos;
using Oficina.Entidades;

namespace Oficina.Negocio;

// carga las secciones de ejemplo si la base estaba vacía, para poder probar la aplicación sin tener que cargar datos a mano
public static class DatosDeEjemplo
{
    public const string ClaveAdministrador = "admin2026";
    public const string ClaveSectores = "oficina2026";

    private record DefSector(string Nombre, string Clave, TipoSector Tipo, string Usuario);

    private static readonly DefSector[] Sectores =
    [
        new("Lotes", "LOT", TipoSector.Lotes, "lotes"),
        new("Entrada y Salida", "EYS", TipoSector.Actas, "entrada"),
        new("Reclamos Vía Pública", "RVP", TipoSector.Reclamos, "reclamos"),
        new("Seguimiento II", "SEG", TipoSector.Actas, "seguimiento"),
        new("Sala Auditoría", "SAL", TipoSector.Actas, "sala"),
        new("Sentencias", "SEN", TipoSector.Actas, "sentencias"),
        new("Apremio", "APR", TipoSector.Actas, "apremio"),
        new("Estadísticas", "EST", TipoSector.Consulta, "estadisticas"),
    ];

    // vuelve verdadero si cargo los datos
    public static bool CargarSiHaceFalta(IDatosSeguridad seg, IDatosRegistros reg)
    {
        if (seg.HayUsuarios()) return false;
        var hoy = DateTime.Today;
        var azar = new Random(2026);

        // sectores roles y permisos, y cuentas de usuario para cada sector
        var sectores = new Dictionary<string, Sector>();
        foreach (var d in Sectores)
        {
            var s = seg.ListarSectores().FirstOrDefault(x => x.Clave == d.Clave) ?? new Sector { Nombre = d.Nombre, Clave = d.Clave, Tipo = d.Tipo };
            if (s.IdSector == 0) seg.GuardarSector(s);
            sectores[d.Clave] = s;
        }

        Usuario Cuenta(string nombre, string clave, Rol rol)
        {
            var (hash, sal) = Contrasenas.Generar(clave);
            var u = new Usuario { NombreUsuario = nombre, ClaveHash = hash, ClaveSal = sal, IdRol = rol.IdRol, Activo = true };
            seg.GuardarUsuario(u);
            return u;
        }

        var rolAdmin = seg.ListarRoles().FirstOrDefault(r => r.EsAdministrador) ?? new Rol { Nombre = "Administrador", EsAdministrador = true };
        if (rolAdmin.IdRol == 0) seg.GuardarRol(rolAdmin);
        var admin = Cuenta("admin", ClaveAdministrador, rolAdmin);

        var usuarios = new Dictionary<string, Usuario>();
        var actas = Sectores.Where(d => d.Tipo == TipoSector.Actas).Select(d => sectores[d.Clave].IdSector).ToList();
        foreach (var d in Sectores)
        {
            var s = sectores[d.Clave];
            var rol = seg.ListarRoles().FirstOrDefault(r => r.Nombre == d.Nombre) ?? new Rol { Nombre = d.Nombre, IdSectorPropio = s.IdSector };
            if (rol.IdRol == 0) seg.GuardarRol(rol);
            usuarios[d.Clave] = Cuenta(d.Usuario, ClaveSectores, rol);

            var permisos = new Dictionary<int, Accion> { [s.IdSector] = d.Tipo == TipoSector.Consulta ? Accion.Leer : Accion.Todas };
            void Leer(IEnumerable<int> ids) { foreach (var id in ids) permisos.TryAdd(id, Accion.Leer); }
            if (d.Tipo == TipoSector.Actas) Leer(actas);                   // ven el recorrido del acta por los otros sectores
            if (d.Clave == "LOT") Leer([sectores["EYS"].IdSector]);       // los lotes salen de Entrada y Salida
            if (d.Tipo == TipoSector.Consulta) Leer(sectores.Values.Select(x => x.IdSector));
            seg.GuardarPermisos(rol.IdRol, permisos.Select(p => new Permiso { IdRol = rol.IdRol, IdSector = p.Key, Acciones = p.Value }));
        }

        void Historial(string entidad, int id, string clave, string accion, string detalle, DateTime fecha) => reg.AgregarHistorial(new EntradaHistorial
        {
            Entidad = entidad, IdEntidad = id, IdSector = clave == null ? null : sectores[clave].IdSector, Accion = accion, Detalle = detalle,
            IdUsuario = clave == null ? admin.IdUsuario : usuarios[clave].IdUsuario, Fecha = fecha
        });
        DateTime Hora(DateTime dia) => dia.Date.AddHours(8 + azar.Next(0, 6)).AddMinutes(azar.Next(0, 60));
        T Al<T>(IList<T> lista) => lista[azar.Next(lista.Count)];

        // listas
        var reparticiones = new[] { "Tránsito", "Inspección General", "Bromatología", "Obras Particulares", "Medio Ambiente" }
            .Select(n => { var r = new Reparticion { Nombre = n }; reg.GuardarReparticion(r); return r; }).ToList();
        var juzgados = new[] { "Juzgado de Faltas N° 1", "Juzgado de Faltas N° 2", "Juzgado de Faltas N° 3" }
            .Select(n => { var j = new Juzgado { Nombre = n }; reg.GuardarJuzgado(j); return j; }).ToList();

        // personas
        var nombres = new (string Apellido, string Nombre)[]
        {
            ("Fernández", "Jorge"), ("Rodríguez", "Ana Laura"), ("López", "Carlos Alberto"), ("Martínez", "Silvia"), ("Pérez", "Hugo Daniel"),
            ("Sosa", "Graciela"), ("Romero", "Martín"), ("Díaz", "Valeria"), ("Acosta", "Ricardo"), ("Medina", "Lucía"), ("Suárez", "Óscar"),
            ("Benítez", "Claudia"), ("Herrera", "Pablo"), ("Aguirre", "Natalia"), ("Molina", "Sergio"), ("Castro", "Mariela")
        };
        var personas = nombres.Select(n =>
        {
            var p = new Persona { Apellido = n.Apellido, Nombre = n.Nombre, TipoDocumento = "DNI", NroDocumento = azar.Next(14_000_000, 44_000_000).ToString() };
            reg.GuardarPersona(p);
            return p;
        }).ToList();
        foreach (var (razon, cuit) in new[] { ("Transportes del Sur S.A.", "30712345678"), ("Cooperativa El Progreso", "30598765432") })
        {
            var p = new Persona { Apellido = razon, TipoDocumento = "CUIT", NroDocumento = cuit };
            reg.GuardarPersona(p);
            personas.Add(p);
        }
        var gomez = new Persona { Apellido = "Gómez", Nombre = "María Elena", TipoDocumento = "DNI", NroDocumento = "27845123" };
        reg.GuardarPersona(gomez);

        // actas y su recorrido por el sector
        var pasos = new (string Clave, string[] Detalles)[]
        {
            ("EYS", ["Ingreso de acta", "Recepción de acta por remito"]),
            ("SEG", ["Pase a Seguimiento II", "Control de plazos", "Pedido de informe"]),
            ("SAL", ["Auditoría de acta", "Revisión en Sala", "Observada por Sala"]),
            ("SEN", ["Sentencia dictada", "Notificación de sentencia", "Apelación presentada"]),
            ("APR", ["Inicio de juicio de apremio", "Plan de pago acordado", "Embargo trabado"]),
        };
        string[] observaciones = ["", "", "", "Se notificó por cédula.", "Falta documentación del titular.", "Con dictamen de Asesoría Letrada.", "Reitera descargo."];

        Acta NuevaActa(string causa, Reparticion rep, string nroActa, Persona persona, DateTime fecha, decimal? monto1, decimal? monto2, string obs)
        {
            var a = new Acta
            {
                NroCausa = causa, IdReparticion = rep.IdReparticion, Anio = fecha.Year, NroActa = nroActa, IdJuzgado = Al(juzgados).IdJuzgado,
                IdPersona = persona?.IdPersona, FechaActa = fecha, Monto1 = monto1, Monto2 = monto2, Observaciones = obs, Version = 1,
                IdUsuarioAlta = usuarios["EYS"].IdUsuario, FechaAlta = Hora(fecha.AddDays(1))
            };
            reg.GuardarActa(a);
            reg.AgregarVersion(ActaVersion.Desde(a, 1, "Acta original", a.IdUsuarioAlta, a.FechaAlta));
            Historial("Acta", a.IdActa, "EYS", "Alta", $"Causa {causa} · acta {nroActa}", a.FechaAlta);
            return a;
        }

        void Recorrido(Acta a, int cantidadPasos, DateTime desde, string[] detallesFijos = null)
        {
            var fecha = desde;
            for (int i = 0; i < cantidadPasos; i++)
            {
                fecha = fecha.AddDays(azar.Next(3, 25));
                if (fecha > hoy) break;
                var (clave, detalles) = pasos[i];
                var obs = Al(observaciones);
                var m = new Movimiento
                {
                    IdActa = a.IdActa, IdSector = sectores[clave].IdSector, Fecha = fecha,
                    Detalle = detallesFijos?[i] ?? Al(detalles),
                    Remito = clave == "EYS" || azar.Next(3) == 0 ? $"R-{azar.Next(1000, 9999)}" : null,
                    Observaciones = obs == "" ? null : obs,
                    IdUsuarioAlta = usuarios[clave].IdUsuario, FechaAlta = Hora(fecha)
                };
                reg.GuardarMovimiento(m);
                Historial("Movimiento", m.IdMovimiento, clave, "Alta", $"Causa {a.NroCausa}: {m.Detalle}", m.FechaAlta);
            }
        }

        int causa = 3100;
        for (int i = 0; i < 40; i++)
        {
            var fecha = hoy.AddDays(-azar.Next(30, 420));
            var obs = Al(observaciones);
            var a = NuevaActa($"{causa += azar.Next(1, 40)}", Al(reparticiones), azar.Next(1000, 1040).ToString(),   // el N° de acta se repite
                azar.Next(8) == 0 ? null : Al(personas), fecha, azar.Next(5, 120) * 1000m,
                azar.Next(3) == 0 ? azar.Next(1, 30) * 500m : null, obs == "" ? null : obs);
            Recorrido(a, azar.Next(1, pasos.Length + 1), fecha);
        }

        // El caso que muestra la búsqueda general: la misma acta en todos los sectores, y modificada una vez.
        var fechaCaso = hoy.AddDays(-150);
        var caso = NuevaActa("4512", reparticiones[0], "1187", gomez, fechaCaso, 85_000m, null, "Estacionamiento en lugar prohibido.");
        Recorrido(caso, pasos.Length, fechaCaso,
            ["Ingreso de acta", "Pase a Seguimiento II", "Observada por Sala", "Sentencia dictada", "Inicio de juicio de apremio"]);
        // Otra acta con el mismo N° (1187) en otra repartición: lo que identifica es causa + repartición + año
        var otra = NuevaActa("4630", reparticiones[2], "1187", personas[3], hoy.AddDays(-60), 42_000m, null, null);
        Recorrido(otra, 2, hoy.AddDays(-60));

        var modif = Hora(fechaCaso.AddDays(70));
        if (modif < hoy)
        {
            caso.Monto1 = 64_500m;
            caso.Monto2 = 12_000m;
            caso.Observaciones = "Estacionamiento en lugar prohibido. Monto reducido por resolución de Sala.";
            caso.Version = 2;
            caso.IdUsuarioModif = usuarios["SAL"].IdUsuario;
            caso.FechaModif = modif;
            reg.GuardarActa(caso);
            reg.AgregarVersion(ActaVersion.Desde(caso, 2, "Corrección del monto según resolución de Sala", caso.IdUsuarioModif.Value, modif));
            Historial("Acta", caso.IdActa, "SAL", "Modificación",
                "Versión 2 · Corrección del monto según resolución de Sala · Monto 1: 85.000,00 → 64.500,00; Monto 2: (vacío) → 12.000,00", modif);
        }

        // reclamos para autos abandonados (puede cambiar deepende com se defina para la base )
        string[] calles = ["Av. San Martín", "Belgrano", "Mitre", "Rivadavia", "Sarmiento", "9 de Julio", "Moreno", "Urquiza"];
        string[] vehiculos = ["Fiat Duna blanco", "Renault 12 gris", "Ford Falcon celeste", "VW Gol rojo, sin patente", "Peugeot 504 verde",
                              "Chevrolet Corsa negro", "Camioneta F-100 azul", null];
        string[] inspectores = ["Inspector Ramírez", "Inspectora Godoy", "Inspector Vera"];
        for (int i = 1; i <= 14; i++)
        {
            var fecha = hoy.AddDays(-azar.Next(2, 200));
            bool verificado = azar.Next(3) > 0 && fecha < hoy.AddDays(-3);
            var r = new Reclamo
            {
                NroCaso = $"RVP-{fecha.Year}-{i:000}", Fecha = fecha, Ubicacion = $"{Al(calles)} {azar.Next(100, 2400)}", Vehiculo = Al(vehiculos),
                Verificado = verificado, FechaVerificacion = verificado ? fecha.AddDays(azar.Next(1, 3)) : null,
                VerificadoPor = verificado ? Al(inspectores) : null,
                Observaciones = azar.Next(3) == 0 ? "Vecino reitera el aviso." : null,
                IdUsuarioAlta = usuarios["RVP"].IdUsuario, FechaAlta = Hora(fecha)
            };
            reg.GuardarReclamo(r);
            Historial("Reclamo", r.IdReclamo, "RVP", "Alta", $"Caso {r.NroCaso} · {r.Ubicacion}", r.FechaAlta);
        }

        // lotes
        for (int i = 1; i <= 6; i++)
        {
            var fecha = hoy.AddDays(-30 * (7 - i) - azar.Next(0, 10));
            var l = new Lote
            {
                NroLote = $"L-{fecha.Year}-{i:00}", Fecha = fecha, CantTransito = azar.Next(20, 120), CantCompetencia = azar.Next(0, 30),
                CantPrescriptas = azar.Next(0, 15), Observaciones = i == 3 ? "Faltan 2 actas de Bromatología." : null,
                IdUsuarioAlta = usuarios["LOT"].IdUsuario, FechaAlta = Hora(fecha)
            };
            reg.GuardarLote(l);
            Historial("Lote", l.IdLote, "LOT", "Alta", $"Lote {l.NroLote} · {l.Total} actas", l.FechaAlta);
        }
        return true;
    }
}
