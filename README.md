# Tecnova Gestión de Proyectos

Sistema de proyectos y tareas para Tecnova. Utiliza ASP.NET MVC 5.3, ASP.NET Web API 2 (paquetes 5.3), .NET Framework 4.8, ADO.NET y SQL Server. La interfaz consume la API por HTTP; solamente la API tiene conexión a SQL Server.

## Requisitos

* Windows con Visual Studio 2022 o Build Tools y las herramientas de compilación web/.NET Framework 4.8, incluido el Targeting Pack.
* IIS Express y SQL Server. Se ha probado con SQL Server 2022 Express LocalDB, instancia `(localdb)\MSSQLLocalDB`.
* PowerShell. Las pruebas automatizadas requieren PowerShell 7 (`pwsh`).
* Internet durante la primera restauración desde NuGet. Las versiones están fijadas en `packages.lock.json`.

VS Code puede utilizarse como editor, pero sus extensiones no sustituyen MSBuild, .NET Framework ni SQL Server. El ZIP no instala estas herramientas. Este proyecto no se inicia con Live Server ni con `dotnet run`.

## Instalación desde GitHub

1. Descargar **Code → Download ZIP** y descomprimir. Abrir en VS Code la carpeta que contiene `Tecnova.Proyectos.sln`, `CONFIGURAR.ps1` e `INICIAR.ps1`.
2. Abrir una terminal de PowerShell en esa carpeta. Si se usa LocalDB 2022, puede iniciarse con:

```powershell
& 'C:\Program Files\Microsoft SQL Server\160\Tools\Binn\SqlLocalDB.exe' start MSSQLLocalDB
```

3. Preparar la base de datos:

```powershell
.\CONFIGURAR.ps1
```

Esto ejecuta **el único script SQL**, `Database/CrearBaseDatos.sql`, y configura la conexión de la API. Crea `TecnovaProyectos`, siete tablas, nueve claves foráneas, índices, catálogos y dos cuentas de demostración. Puede repetirse sin borrar los datos. Las cuentas se insertan únicamente si `Usuarios` está vacía; repetirlo no cambia contraseñas existentes.

Como alternativa, ejecutar ese mismo archivo SQL desde SQL Server Management Studio en la instancia elegida y ajustar la conexión `TecnovaProyectos` en `Tecnova.Proyectos.Api/Web.config`.

Para otra instancia con autenticación integrada de Windows:

```powershell
.\CONFIGURAR.ps1 -Servidor 'SERVIDOR\INSTANCIA'
```

4. Compilar e iniciar ambas aplicaciones:

```powershell
.\INICIAR.ps1
```

5. Abrir **http://localhost:51850/**. La API se ejecuta en http://localhost:51851 y MVC la consume automáticamente. Mantener abierta la terminal; **Ctrl+C** detiene los dos procesos iniciados por el script. Para volver a iniciar, ejecutar el mismo comando.

Para compilar sin iniciar: `./INICIAR.ps1 -SoloCompilar`. Para otros puertos:

```powershell
.\INICIAR.ps1 -PuertoWeb 51852 -PuertoApi 51853
```

El lanzador configura la URL de la API en MVC y crea configuraciones locales independientes de IIS Express en `.local/`. Esta carpeta, los logs y los binarios no se publican. Cambiar de carpeta o puerto no cambia la base: dos copias conectadas a `TecnovaProyectos` en la misma instancia utilizan los mismos datos.

## Accesos de demostración

Son cuentas públicas para la evaluación local. La base nueva comienza sin proyectos ni tareas para permitir probar el registro completo.

| Rol | Correo | Contraseña |
|---|---|---|
| Administrador | `admin.demo@tecnova.local` | `Tecnova.Admin!2026` |
| Usuario | `usuario.demo@tecnova.local` | `Tecnova.Usuario!2026` |

El administrador puede crear colaboradores y administradores adicionales desde **Usuarios**. Los usuarios, proyectos y tareas nuevos quedan en el SQL Server de quien ejecuta la aplicación, no en GitHub.

## Recorrido de uso

1. Ingresar como administrador y revisar **Usuarios**. Crear o editar los colaboradores que participarán.
2. Abrir **Proyectos → Nuevo proyecto**, indicar fechas, elegir un administrador responsable y marcar integrantes. El responsable se incluye automáticamente.
3. Abrir **Ver proyecto → Nueva tarea** y elegir un integrante activo, estado, prioridad y vencimiento dentro del intervalo del proyecto.
4. Ingresar como colaborador. Consultar sus proyectos y **Mis tareas**; editar las tareas que tenga asignadas. Un integrante puede crear y asignar tareas en sus proyectos; después, solamente el asignado o un administrador las modifica.
5. Consultar el **Panel general**. Sus conteos se calculan en la API y se actualizan al entrar; las listas se consultan de nuevo después de cada operación Ajax.
6. Cuando no queden tareas pendientes ni en progreso, el administrador puede editar el proyecto y cambiarlo a **Cerrado**. El proyecto cerrado queda de solo lectura.

### Reglas de negocio

* Administrador: administra usuarios y todos los proyectos/tareas. Usuario: accede a los proyectos en los que participa; modifica o elimina solamente sus tareas asignadas.
* Estados de tarea: Pendiente, En progreso, Completada y Cancelada. Se pueden cambiar mientras el proyecto permanezca activo. Prioridades: Alta, Media y Baja.
* Solamente se eliminan tareas pendientes. Para conservar trabajo iniciado, se utiliza Cancelada. Solamente se eliminan proyectos activos sin tareas.
* No se desactiva un usuario con tareas pendientes/en progreso ni un responsable de proyectos activos. Primero debe reasignarse el trabajo. No se permite desactivar o quitar el rol administrador de la propia cuenta.
* No se retira de un proyecto a un integrante que tenga tareas asignadas, incluso finalizadas: se debe conservar o reasignar esas tareas.
* Cambiar contraseña, rol o estado activo invalida las sesiones anteriores de ese usuario. La desactivación conserva los registros relacionados.
* Las operaciones comparan un campo `Version`. Si alguien guardó antes, se rechaza la versión anterior para evitar sobrescribir cambios.
* Se muestran proyectos activos, tareas por estado, tareas asignadas al usuario y tareas vencidas. El porcentaje de avance es completadas/total de tareas visibles; incluye canceladas en el denominador y es cero cuando no hay tareas. Vencidas usa la fecha UTC y excluye completadas/canceladas.

## Arquitectura y seguridad

```text
Navegador → controladores MVC → cliente HTTP → API REST → ADO.NET → SQL Server
```

Se utiliza **Session**, una de las alternativas de gestión de estado: ASP.NET conserva la identidad en la API; MVC guarda en su propia sesión la cookie de la API. Esa cookie no se entrega al navegador. Las sesiones son InProc, con 30 minutos de inactividad: reiniciar la aplicación requiere iniciar sesión nuevamente, pero no pierde los datos SQL.

La API comprueba cuenta activa, rol y participación en cada solicitud. Las contraseñas se derivan con PBKDF2-HMAC-SHA256, sal aleatoria y 600.000 iteraciones. Cinco intentos fallidos bloquean la cuenta durante 15 minutos. Se incluyen tokens antifalsificación en MVC, cabecera propia en las escrituras a la API, cookies HttpOnly/SameSite, consultas parametrizadas, validaciones del servidor, transacciones y cabeceras CSP. No se habilita CORS; el navegador consume MVC, no la API directamente.

Los formularios y confirmaciones son del sistema, sin `alert` ni `confirm` del navegador. Se utilizan etiquetas, navegación por teclado, estados de carga y avisos de error. La interfaz se adapta a pantallas pequeñas y el menú se sitúa a la izquierda en escritorio.

### Endpoints de la API

| Método | Ruta | Operación |
|---|---|---|
| GET | `/api/estado` | Disponibilidad del servicio, sin datos privados. |
| POST | `/api/auth/login` | Autenticar y establecer sesión. |
| GET | `/api/auth/me` | Consultar identidad vigente. |
| POST | `/api/auth/logout` | Invalidar sesión. |
| GET | `/api/catalogos` | Roles, estados y prioridades. |
| GET / POST | `/api/usuarios` | Listar / crear, solo administrador. |
| GET / PUT / DELETE | `/api/usuarios/{id}` | Consultar / editar / desactivar. |
| GET / POST | `/api/proyectos` | Listar / crear. |
| GET / PUT / DELETE | `/api/proyectos/{id}` | Detalle / editar o cerrar / eliminar vacío. |
| GET / POST | `/api/tareas` | Listar / crear. GET acepta `?proyecto={id}`. |
| GET / PUT / DELETE | `/api/tareas/{id}` | Consultar / editar / eliminar pendiente. |
| GET | `/api/indicadores` | Indicadores según el alcance del usuario. |

Las escrituras requieren `X-Tecnova-Client` y los cuerpos usan JSON. Mantener la cookie de sesión recibida en login. PUT incluye `Version` y DELETE recibe `?version={numero}`. Los scripts de integración muestran ejemplos completos. La API devuelve 400 para datos inválidos, 401 sin sesión, 403 sin permiso, 404 para recursos no disponibles y 409 para conflictos.

## Pruebas

Con ambas aplicaciones iniciadas y las cuentas demo sin modificar:

```powershell
pwsh -NoProfile -File .\Tests\Conexion.ps1
pwsh -NoProfile -File .\Tests\Esquema.ps1
pwsh -NoProfile -File .\Tests\Integracion.ps1
```

`Esquema.ps1` crea una base temporal de nombre único, comprueba tablas/relaciones/cuentas, repite la instalación y elimina únicamente esa base. `Integracion.ps1` crea usuarios, proyectos y tareas con identificadores únicos, prueba CRUD, permisos, invalidación, concurrencia, indicadores y el cliente MVC; retira sus propios registros en un bloque de limpieza. Puede aumentar la secuencia de identificadores. No elimina datos creados por el usuario.

Para otros puertos, pasar `-Web http://localhost:PUERTO -Api http://localhost:PUERTO` a las pruebas de conexión/integración. Para otra instancia SQL, pasar `-Servidor` a esquema e integración.

## Solución de problemas

* **NuGet no puede cargar el índice:** comprobar acceso a `https://api.nuget.org/v3/index.json`, red o proxy y repetir la compilación. No significa que falte una extensión de VS Code.
* **Falta MSBuild, .NET Framework o IIS Express:** completar los componentes indicados en Requisitos desde el instalador de Visual Studio.
* **Puerto ocupado:** detener la propia ejecución con Ctrl+C o elegir otro par de puertos. No finalizar procesos desconocidos.
* **No se abre TecnovaProyectos:** iniciar la instancia SQL, ejecutar `CONFIGURAR.ps1` con el mismo usuario Windows y revisar `connectionStrings` de la API.
* **El archivo MDF ya existe:** no borrar archivos; revisar en SQL Server si la base está desconectada o pertenece a otra instancia y respaldar antes de recuperar. El script no sobrescribe archivos existentes.
* **Las cuentas demo no ingresan:** si ya había usuarios, el script conserva sus accesos. No cambia contraseñas existentes ni reinicia datos.
* **Formulario o sesión vencidos:** volver a iniciar sesión o recargar el formulario. Los datos guardados previamente permanecen en SQL.
* **El servicio no está disponible:** comprobar que ambos procesos sigan iniciados y revisar los archivos `.local/*.log` y `.local/*.error.log`.

## Estructura

- `Tecnova.Proyectos.Web`: controladores MVC, vistas Razor, cliente HTTP, CSS y JavaScript.
- `Tecnova.Proyectos.Api`: controladores REST, acceso ADO.NET y seguridad.
- `Database/CrearBaseDatos.sql`: instalación completa del esquema y datos iniciales.
- `CONFIGURAR.ps1`: ejecución del SQL y configuración de la instancia.
- `INICIAR.ps1`: restauración, compilación e inicio local de ambas aplicaciones.
- `Tests`: comprobaciones de esquema, comunicación, integración y seguridad funcional.

## Alcance

Las direcciones localhost son locales, no alojamiento público. GitHub distribuye el código; cada persona ejecuta su propia copia. Para un despliegue empresarial se requieren HTTPS, configuración de infraestructura, administración de secretos y sesiones compartidas si hay varias instancias.

Esta versión no incluye adjuntos, correos, notificaciones en tiempo real, paginación en servidor ni auditoría histórica completa. Actualiza los datos al entrar en las secciones y después de operar; no transmite automáticamente cambios entre navegadores. Las pruebas funcionales no constituyen una auditoría de seguridad ni una prueba de carga.
