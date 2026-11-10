param([string]$Servidor='(localdb)\MSSQLLocalDB')
$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Data
$builder=New-Object System.Data.SqlClient.SqlConnectionStringBuilder
$builder['Data Source']=$Servidor
$builder['Initial Catalog']='master'
$builder['Integrated Security']=$true
$connection=New-Object System.Data.SqlClient.SqlConnection($builder.ConnectionString)
try {
 $connection.Open()
 $sql=Get-Content (Join-Path $PSScriptRoot 'Database/CrearBaseDatos.sql') -Raw
 foreach($batch in [regex]::Split($sql,'(?im)^\s*GO\s*$')){
  if($batch.Trim()){$command=$connection.CreateCommand();try{$command.CommandText=$batch;$command.CommandTimeout=90;[void]$command.ExecuteNonQuery()}finally{$command.Dispose()}}
 }
}finally{$connection.Dispose()}
$file=Join-Path $PSScriptRoot 'Tecnova.Proyectos.Api/Web.config'
[xml]$config=Get-Content $file
$builder['Initial Catalog']='TecnovaProyectos'
$config.configuration.connectionStrings.add.connectionString=$builder.ConnectionString
$config.Save($file)
Write-Host 'TecnovaProyectos preparada. El script conserva los datos y usuarios existentes.'
