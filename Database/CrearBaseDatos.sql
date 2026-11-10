IF DB_ID(N'TecnovaProyectos') IS NULL
    CREATE DATABASE TecnovaProyectos;
GO
USE TecnovaProyectos;
GO
SET XACT_ABORT ON;
BEGIN TRANSACTION;
IF OBJECT_ID(N'dbo.Roles') IS NULL
BEGIN
 CREATE TABLE dbo.Roles(Id int PRIMARY KEY,Nombre nvarchar(30) NOT NULL UNIQUE,Descripcion nvarchar(200) NOT NULL);
 CREATE TABLE dbo.Usuarios(Id int IDENTITY PRIMARY KEY,Nombre nvarchar(120) NOT NULL,Email nvarchar(180) NOT NULL UNIQUE,ContrasenaHash varchar(200) NOT NULL,IdRol int NOT NULL REFERENCES dbo.Roles(Id),FechaRegistro datetime2 NOT NULL DEFAULT SYSUTCDATETIME(),Activo bit NOT NULL DEFAULT 1,Version int NOT NULL DEFAULT 1,SecurityStamp uniqueidentifier NOT NULL DEFAULT NEWID(),Intentos int NOT NULL DEFAULT 0,BloqueadoHasta datetime2 NULL);
 CREATE TABLE dbo.Estados(Id int PRIMARY KEY,Nombre nvarchar(30) NOT NULL,EntidadAplicacion varchar(10) NOT NULL,CONSTRAINT UQ_EstadoEntidad UNIQUE(Id,EntidadAplicacion));
 CREATE TABLE dbo.Prioridades(Id int PRIMARY KEY,Nombre nvarchar(20) NOT NULL UNIQUE);
 CREATE TABLE dbo.Proyectos(Id int IDENTITY PRIMARY KEY,Nombre nvarchar(150) NOT NULL,Descripcion nvarchar(3000) NOT NULL,FechaInicio date NOT NULL,FechaFinEstimada date NOT NULL,IdEstado int NOT NULL,EntidadAplicacion varchar(10) NOT NULL DEFAULT 'Proyecto' CHECK(EntidadAplicacion='Proyecto'),IdResponsable int NOT NULL REFERENCES dbo.Usuarios(Id),FechaCreacion datetime2 NOT NULL DEFAULT SYSUTCDATETIME(),Version int NOT NULL DEFAULT 1,FOREIGN KEY(IdEstado,EntidadAplicacion) REFERENCES dbo.Estados(Id,EntidadAplicacion),CHECK(FechaFinEstimada>=FechaInicio));
 CREATE TABLE dbo.ProyectoMiembros(IdProyecto int NOT NULL REFERENCES dbo.Proyectos(Id),IdUsuario int NOT NULL REFERENCES dbo.Usuarios(Id),PRIMARY KEY(IdProyecto,IdUsuario));
 CREATE TABLE dbo.Tareas(Id int IDENTITY PRIMARY KEY,IdProyecto int NOT NULL REFERENCES dbo.Proyectos(Id),Titulo nvarchar(150) NOT NULL,Descripcion nvarchar(3000) NOT NULL,IdAsignado int NOT NULL,IdEstado int NOT NULL,EntidadAplicacion varchar(10) NOT NULL DEFAULT 'Tarea' CHECK(EntidadAplicacion='Tarea'),IdPrioridad int NOT NULL REFERENCES dbo.Prioridades(Id),FechaCreacion datetime2 NOT NULL DEFAULT SYSUTCDATETIME(),FechaVencimiento date NOT NULL,Version int NOT NULL DEFAULT 1,FOREIGN KEY(IdEstado,EntidadAplicacion) REFERENCES dbo.Estados(Id,EntidadAplicacion),FOREIGN KEY(IdProyecto,IdAsignado) REFERENCES dbo.ProyectoMiembros(IdProyecto,IdUsuario));
 CREATE INDEX IX_Miembros_Usuario ON dbo.ProyectoMiembros(IdUsuario,IdProyecto);
 CREATE INDEX IX_Proyectos_Estado ON dbo.Proyectos(IdEstado) INCLUDE(Nombre,IdResponsable);
 CREATE INDEX IX_Tareas_ProyectoEstado ON dbo.Tareas(IdProyecto,IdEstado) INCLUDE(IdAsignado,IdPrioridad,FechaVencimiento);
 CREATE INDEX IX_Tareas_AsignadoEstado ON dbo.Tareas(IdAsignado,IdEstado) INCLUDE(IdProyecto,FechaVencimiento);
END;
IF NOT EXISTS(SELECT 1 FROM dbo.Roles)
 INSERT dbo.Roles VALUES(1,N'Administrador',N'Gestión de usuarios y proyectos'),(2,N'Usuario',N'Colaborador de proyectos');
IF NOT EXISTS(SELECT 1 FROM dbo.Estados)
 INSERT dbo.Estados VALUES(1,N'Activo','Proyecto'),(2,N'Cerrado','Proyecto'),(10,N'Pendiente','Tarea'),(11,N'En progreso','Tarea'),(12,N'Completada','Tarea'),(13,N'Cancelada','Tarea');
IF NOT EXISTS(SELECT 1 FROM dbo.Prioridades)
 INSERT dbo.Prioridades VALUES(1,N'Alta'),(2,N'Media'),(3,N'Baja');
IF NOT EXISTS(SELECT 1 FROM dbo.Usuarios WITH(UPDLOCK,HOLDLOCK))
 INSERT dbo.Usuarios(Nombre,Email,ContrasenaHash,IdRol) VALUES
 (N'Administrador Tecnova','admin.demo@tecnova.local','v1$600000$ORFoHUke1knBBMcxjHXLJA==$efE/1cvxNwfx/vGYvF6LeF/zRxCRiByJLPBokQXd61A=',1),
 (N'Colaborador Tecnova','usuario.demo@tecnova.local','v1$600000$gAuRPiVemy/u1jfhkmWxgg==$hMOPQc/JHLQpvHBFBxC9D+7sMuYYdCyhk5l1iUFcBXE=',2);
COMMIT;
GO
