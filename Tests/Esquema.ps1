#requires -Version 7.0
param([string]$Servidor='(localdb)\MSSQLLocalDB')
$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Data
$name='TecnovaPrueba_'+[guid]::NewGuid().ToString('N')
if($name -notmatch '^TecnovaPrueba_[0-9a-f]{32}$'){throw 'Nombre temporal inválido'}
$builder=New-Object System.Data.SqlClient.SqlConnectionStringBuilder
$builder['Data Source']=$Servidor;$builder['Initial Catalog']='master';$builder['Integrated Security']=$true
$conn=New-Object System.Data.SqlClient.SqlConnection($builder.ConnectionString)
function Execute([string]$sql){$cmd=$conn.CreateCommand();try{$cmd.CommandText=$sql;$cmd.CommandTimeout=90;[void]$cmd.ExecuteNonQuery()}finally{$cmd.Dispose()}}
function Scalar([string]$sql){$cmd=$conn.CreateCommand();try{$cmd.CommandText=$sql;return $cmd.ExecuteScalar()}finally{$cmd.Dispose()}}
try{
 $conn.Open()
 $source=Get-Content (Join-Path (Split-Path $PSScriptRoot) 'Database/CrearBaseDatos.sql') -Raw
 $sql=$source.Replace('TecnovaProyectos',$name)
 foreach($batch in [regex]::Split($sql,'(?im)^\s*GO\s*$')){if($batch.Trim()){Execute $batch}}
 if((Scalar 'SELECT COUNT(*) FROM sys.tables') -ne 8){throw 'Se esperaban ocho tablas.'}
 if((Scalar 'SELECT COUNT(*) FROM sys.foreign_keys') -ne 9){throw 'Se esperaban nueve claves foráneas.'}
 if((Scalar 'SELECT COUNT(*) FROM Usuarios') -ne 2){throw 'No se crearon las cuentas de demostración.'}
 if((Scalar 'SELECT COUNT(*) FROM Estados') -ne 6){throw 'Catálogo de estados incompleto.'}
 Execute "UPDATE Usuarios SET Nombre=N'Nombre conservado' WHERE Email='usuario.demo@tecnova.local'"
 foreach($batch in [regex]::Split($sql,'(?im)^\s*GO\s*$')){if($batch.Trim()){Execute $batch}}
 if((Scalar "SELECT Nombre FROM Usuarios WHERE Email='usuario.demo@tecnova.local'") -ne 'Nombre conservado'){throw 'El script sobrescribió datos.'}
 if((Scalar 'SELECT COUNT(*) FROM Usuarios') -ne 2){throw 'Se duplicaron cuentas.'}
 try{Execute "INSERT Tareas(IdProyecto,Titulo,Descripcion,IdAsignado,IdEstado,IdPrioridad,FechaVencimiento) VALUES(-1,'Prueba','Prueba',-1,10,1,'2026-12-01')";throw 'Se permitió una referencia inválida.'}catch{if($_.Exception.Message -like '*Se permitió*'){throw}}
 Write-Host 'PASS: instalación limpia, ocho tablas, nueve relaciones, cuentas demo e instalación repetible.'
}finally{
 if($conn.State -eq 'Open'){$conn.ChangeDatabase('master');Execute "IF DB_ID(N'$name') IS NOT NULL BEGIN ALTER DATABASE [$name] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [$name]; END"}
 $conn.Dispose()
}
