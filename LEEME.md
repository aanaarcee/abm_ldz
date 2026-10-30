# Registros de Oficina · prototipo de escritorio

Aplicación de Windows Forms en C# (.NET 10) con SQL Server, en cuatro capas, para reemplazar las planillas de Google Sheets de cada sector.

## Cómo se arma

| Capa | Proyecto | Qué tiene |
|---|---|---|
| Entidades | `src/Oficina.Entidades` | Clases del dominio: Sector, Rol, Usuario, Permiso, Acta, ActaVersion, Movimiento, Reclamo, Lote, historial. |
| Datos | `src/Oficina.Datos` | Interfaces `IDatosSeguridad` e `IDatosRegistros`, con dos implementaciones: SQL Server (`SqlServer/`, Microsoft.Data.SqlClient) y en memoria (`Memoria/`, para probar sin base). |
| Negocio | `src/Oficina.Negocio` | Ingreso con contraseña (PBKDF2), permisos por sector, servicios que validan, controlan permisos y escriben el historial, búsqueda, administración, exportación a CSV y datos de ejemplo. `Aplicacion` es la única puerta de entrada que usa la interfaz. |
| Interfaz | `src/Oficina.UI` | Pantallas armadas en código (sin diseñador). El programa se llama `RegistrosOficina.exe`. |

## Ponerlo en marcha

1. Si el servidor no es el local, cambiar `src/Oficina.UI/conexion.txt` (se copia al lado del programa). Si la cadena no dice `Database`, se usa `OficinaRegistros`.
2. Abrir `Oficina.sln` en Visual Studio y apretar F5, o por consola:

```bash
dotnet build Oficina.sln
```

```bash
dotnet run --project src/Oficina.UI
```

La primera vez, si la base no existe, la aplicación la crea con `sql/OficinaRegistros.sql` (va incrustado en el programa), y si no tiene cuentas carga sola los datos de ejemplo. Si no puede conectarse, ofrece abrir en **modo prueba** (datos en memoria que se pierden al cerrar). También se puede forzar con `--memoria`.

## Cuentas de ejemplo

| Cuenta | Contraseña | Puede |
|---|---|---|
| `admin` | `admin2026` | Todo, en todos los sectores, y la administración. |
| `entrada`, `seguimiento`, `sala`, `sentencias`, `apremio` | `oficina2026` | Todo en su sector; ver los otros sectores de actas (para el recorrido del acta). |
| `lotes` | `oficina2026` | Todo en Lotes; ver Entrada y Salida. |
| `reclamos` | `oficina2026` | Todo en Reclamos Vía Pública. |
| `estadisticas` | `oficina2026` | Ver todos los sectores. |

Conviene cambiar estas contraseñas desde **Configuración → Cuentas** antes de usarlo en serio.

## Qué se puede probar

- **Buscar en todo** (arriba): busca mientras se escribe, sin mayúsculas ni acentos ("gomez" encuentra "Gómez"), en causa, acta, persona, DNI, remito, movimiento, observaciones, reclamos y lotes. Los resultados salen agrupados por sector. Esc borra la búsqueda; Ctrl+F vuelve al buscador.
- **Buscar dentro de un sector**: cada planilla tiene su propio buscador.
- **Caso de ejemplo**: buscar `4512`. Es la misma acta pasando por los cinco sectores de actas, y fue modificada una vez: en la ficha se ve la versión original contra la actual. Buscar `1187` muestra dos actas distintas con el mismo N° de acta (lo que las distingue es causa + repartición + año).
- **Nuevo / Modificar / Eliminar / Exportar CSV**: los botones aparecen según los permisos de la cuenta. Eliminar es una baja lógica: la fila deja de verse pero queda guardada, con el motivo en el historial.
- **Ficha del acta** (doble clic en una fila o "Ver acta"): datos, recorrido por los sectores, original y actual, versiones e historial. "Modificar acta" pide un motivo y guarda una versión nueva.
- **Cada cuenta ve solo lo suyo**: una cuenta que no es admin no ve los sectores que no puede leer en ningún lado (menú, inicio, búsqueda, fichas, historial, totales, sugerencias), ni los nombres de cuentas de otros sectores, ni una lista de sus permisos: los botones aparecen solo si se pueden usar. Si intenta abrir algo oculto, el mensaje es «Ese registro no existe o no está disponible». Por ejemplo, entrar con `lotes` y buscar `4512`.
- **Configuración** (botón ⚙ arriba, solo existe para `admin`): abre una ventana aparte con roles, permisos por sector, cuentas, sectores, reparticiones y juzgados, e historial de estos cambios. Al cerrarla, el menú de sectores y el inicio se actualizan.

## Respaldo en Drive

"Exportar CSV" guarda la planilla del sector (con el filtro que se esté usando) en un archivo separado por punto y coma, que Google Sheets y Excel abren directo. Si está instalado Google Drive para escritorio, propone guardarlo en la carpeta de Drive (`G:\Mi unidad` o similar), así se sube solo.

## Decisiones que todavía son provisorias

- **Movimientos**: texto libre, con sugerencias, hasta que la oficina pase la lista real.
- **Monto 1 y Monto 2**: se llaman así hasta saber qué son.
- **Sala Auditoría y Sentencias**: están como sectores separados que anotan movimientos sobre la misma acta. Si resultan ser la misma acta con datos propios, hay que agregar esos datos.
- **Lotes**: guardan cantidades por tipo (tránsito, competencia, prescriptas), sin relación con actas.
- Los cambios de permisos y los sectores nuevos se ven cuando la cuenta vuelve a entrar.
- El alta de un acta y su primer movimiento no van en una misma transacción: se valida todo antes de guardar, pero si se corta la conexión en el medio puede quedar el acta sin movimiento.
