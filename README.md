# Tecnova Gestión de Proyectos

Aplicación independiente con ASP.NET MVC 5, ASP.NET Web API 2 y .NET Framework 4.8. La interfaz consume la API por HTTP; no tiene una conexión directa a SQL Server.

## Requisitos

Windows, Visual Studio 2022 o Build Tools con herramientas .NET Framework 4.8, IIS Express, PowerShell y acceso a NuGet para restaurar dependencias. SQL Server será necesario para los módulos de datos.

## Inicio

Abrir PowerShell en la carpeta que contiene la solución y ejecutar `./INICIAR.ps1`. La interfaz se abre en http://localhost:51850 y la API escucha en http://localhost:51851. Mantener la terminal abierta; Ctrl+C detiene los dos procesos iniciados por el script. `./INICIAR.ps1 -SoloCompilar` compila sin iniciar servicios.

Para usar otros puertos: `./INICIAR.ps1 -PuertoWeb 51852 -PuertoApi 51853`. El script configura únicamente la URL de la API en la copia local de Web.config. No cerrar procesos ajenos si un puerto está ocupado.

## Estado actual

Estructura MVC y API separadas, consulta de disponibilidad y pantalla inicial. Aún no están implementados autenticación, base de datos ni los módulos de negocio. No utilizar esta versión inicial con datos reales.

## Estructura

- Tecnova.Proyectos.Web: controladores MVC, vistas y cliente HTTP.
- Tecnova.Proyectos.Api: endpoints REST.
- Tests: comprobaciones de la comunicación entre aplicaciones.

Las direcciones localhost son locales, no alojamiento público. GitHub distribuye el código; cada persona ejecuta su propia copia.
