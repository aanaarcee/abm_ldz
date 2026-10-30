# ABM LDZ · registros de la oficina

Aplicación de escritorio en C# (Windows Forms, POO y N capas) con base de datos SQL Server. Reemplaza las planillas de Google Sheets de cada sector por una base única, con roles por sector y una búsqueda general en todos los registros, o dentro de uno solo.

> En desarrollo. La solución todavía se está armando: ver el estado más abajo.

## Estructura

| Carpeta | Capa | Qué tiene |
| --- | --- | --- |
| `src/Oficina.Entidades` | Entidades | Clases del modelo: Sector, Rol, Usuario, Permiso, Acta, ActaVersion, Movimiento, Lote, Reclamo, Historial. |
| `src/Oficina.Datos` | Datos | Acceso a SQL Server y una versión en memoria para probar sin base. |
| `src/Oficina.Negocio` | Negocio | Reglas: inicio de sesión, permisos por sector, validaciones, historial, búsqueda. |
| `src/Oficina.UI` | Interfaz | Formularios de Windows Forms. |
| `sql/OficinaRegistros.sql` | Base | Crea la base `OficinaRegistros`, sus tablas y el procedimiento de búsqueda. |

## Lo confirmado por la oficina

- Un acta se identifica por **N° de causa + repartición + año**. El N° de acta se puede repetir.
- Existe el **acta original y el acta modificada**: cada cambio guarda una versión.
- Las actas se guardan **al menos 10 años**: no se borra nada, solo se marca como eliminado.
- **Reclamos** son avisos de autos abandonados en la vía pública. «Verificado» es cuando alguien va a ver el auto.
- Quieren seguir usando **Drive como respaldo**: la app exporta a CSV.

Sigue sin definir: la lista de movimientos de un acta, qué son monto 1 y monto 2, y si Sala Auditoría y Sentencias trabajan sobre la misma acta.

## Cómo abrirlo en Visual Studio

Hace falta **Visual Studio 2026** (o una versión que traiga el SDK de **.NET 10**) con la carga de trabajo «Desarrollo de escritorio de .NET», y un **SQL Server** en la misma PC (sirve SQL Server Express o Developer).

1. Clonar el repositorio: en Visual Studio, *Clonar un repositorio* con `https://github.com/aanaarcee/abm_ldz.git`, o por consola:

   ```bash
   git clone https://github.com/aanaarcee/abm_ldz.git
   ```

2. Abrir `Oficina.sln`. El proyecto de inicio es `Oficina.UI`. En la carpeta «Base y documentos» del Explorador de soluciones están el script de la base, este README y el LEEME.
3. Apretar **F5**.

La primera vez, la aplicación:

- se conecta al SQL Server local (`Server=.` con la cuenta de Windows);
- si la base `OficinaRegistros` no existe, la crea sola con el script `sql/OficinaRegistros.sql`, que va incrustado en el programa;
- si la base no tiene cuentas, carga los datos de ejemplo y avisa las cuentas para entrar: `admin` / `admin2026`, y una por sector (`entrada`, `seguimiento`, `sala`, `sentencias`, `apremio`, `lotes`, `reclamos`, `estadisticas`) / `oficina2026`.

Si SQL Server está en otro lado (por ejemplo `.\SQLEXPRESS`), se cambia la cadena en `src/Oficina.UI/conexion.txt`, que se copia al lado del programa. Si no puede conectarse, ofrece seguir en **modo prueba**, con los datos en memoria (se pierden al cerrar).

Detalles de uso, cuentas y qué probar: ver [LEEME.md](LEEME.md).

## Base de datos

La aplicación crea la base sola. Si se prefiere crearla a mano, el script `sql/OficinaRegistros.sql` se puede ejecutar desde SQL Server Management Studio o con:

```
sqlcmd -S . -E -C -i sql/OficinaRegistros.sql
```

## Estado

- [x] Script de la base de datos
- [x] Entidades
- [x] Datos (SQL Server y en memoria), Negocio e Interfaz
- [x] Solución que se abre y corre con F5 al clonar (crea la base y carga datos de ejemplo)
- [ ] Lista definitiva de movimientos, nombres de monto 1 y monto 2, y si Sala Auditoría y Sentencias son la misma acta
