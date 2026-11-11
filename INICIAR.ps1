param([ValidateRange(1024,65535)][int]$PuertoWeb=51850,[ValidateRange(1024,65535)][int]$PuertoApi=51851,[switch]$SoloCompilar)
$ErrorActionPreference='Stop'
if($PuertoWeb -eq $PuertoApi){throw 'La plataforma y la API necesitan puertos distintos.'}
$localRoot=$PSScriptRoot
$vswhere="${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe"
if(!(Test-Path $vswhere)){throw 'Instale Visual Studio o Build Tools con las herramientas .NET Framework 4.8.'}
$msbuild=& $vswhere -latest -products '*' -requires Microsoft.Component.MSBuild -find 'MSBuild\**\Bin\MSBuild.exe' | Select-Object -First 1
if(!$msbuild){throw 'No se encontró MSBuild.'}
& $msbuild "$localRoot\Tecnova.Proyectos.sln" /restore /p:RestoreLockedMode=true /verbosity:minimal /nologo
if($LASTEXITCODE -ne 0){throw 'La restauración o compilación falló. Revise el mensaje anterior.'}
if($SoloCompilar){return}
$iis=Join-Path $env:ProgramFiles 'IIS Express\iisexpress.exe'
if(!(Test-Path $iis)){throw 'Instale IIS Express.'}
foreach($p in @($PuertoWeb,$PuertoApi)){if(Get-NetTCPConnection -LocalPort $p -State Listen -ErrorAction SilentlyContinue){throw "El puerto $p está ocupado. Elija otros puertos o detenga su ejecución anterior."}}
[xml]$apiSettings=Get-Content (Join-Path $localRoot 'Tecnova.Proyectos.Api\Web.config')
Add-Type -AssemblyName System.Data
$database=New-Object System.Data.SqlClient.SqlConnection($apiSettings.configuration.connectionStrings.add.connectionString)
try{$database.Open();$command=$database.CreateCommand();$command.CommandText='SELECT COUNT(*) FROM dbo.Roles';[void]$command.ExecuteScalar();$command.Dispose()}
catch{throw 'No se pudo abrir TecnovaProyectos. Ejecute CONFIGURAR.ps1 y compruebe SQL Server y la conexión en Tecnova.Proyectos.Api/Web.config.'}
finally{$database.Dispose()}
$configFile=Join-Path $localRoot 'Tecnova.Proyectos.Web\Web.config'
[xml]$settings=Get-Content -LiteralPath $configFile
$apiSetting=$settings.configuration.appSettings.add | Where-Object key -eq 'ApiUrl'
if($apiSetting.value -ne "http://localhost:$PuertoApi/"){$apiSetting.value="http://localhost:$PuertoApi/";$settings.Save($configFile)}
$started=@()
$runtime=Join-Path $localRoot '.local'
New-Item -ItemType Directory -Path $runtime -Force | Out-Null
try{
 foreach($entry in @(@('Tecnova.Proyectos.Api',$PuertoApi),@('Tecnova.Proyectos.Web',$PuertoWeb))){
  $appPath=Join-Path $localRoot $entry[0]
  [xml]$hostConfig=Get-Content (Join-Path (Split-Path $iis) 'AppServer\applicationhost.config')
  $site=$hostConfig.configuration.'system.applicationHost'.sites.site
  $site.name=$entry[0]
  $site.application.virtualDirectory.physicalPath=$appPath
  $site.bindings.binding.bindingInformation=":$($entry[1]):localhost"
  $hostPath=Join-Path $runtime ($entry[0]+'.config')
  $hostConfig.Save($hostPath)
  $started+=Start-Process -FilePath $iis -ArgumentList @("/config:`"$hostPath`"","/site:$($entry[0])",'/systray:false') -WindowStyle Hidden -PassThru -RedirectStandardOutput (Join-Path $runtime ($entry[0]+'.log')) -RedirectStandardError (Join-Path $runtime ($entry[0]+'.error.log'))
 }
 Write-Host "Tecnova: http://localhost:$PuertoWeb/ | API: http://localhost:$PuertoApi/api/estado"
 Write-Host 'Mantenga esta terminal abierta. Ctrl+C detiene ambas aplicaciones.'
 while($true){Start-Sleep -Seconds 1;foreach($server in $started){if($server.HasExited){throw 'Una aplicación se detuvo. Revise IIS Express y los puertos.'}}}
}finally{foreach($server in $started){if(!$server.HasExited){Stop-Process -Id $server.Id -ErrorAction SilentlyContinue}}}
