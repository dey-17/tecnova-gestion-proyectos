param([string]$Web='http://localhost:51850',[string]$Api='http://localhost:51851')
$ErrorActionPreference='Stop'
$status=Invoke-RestMethod "$Api/api/estado"
if($status.disponible -ne $true){throw 'La API no respondió correctamente.'}
$page=Invoke-WebRequest "$Web/" -UseBasicParsing
if($page.Content -notmatch 'API conectada'){throw 'MVC no confirmó la conexión con la API.'}
if($page.Headers['X-Content-Type-Options'] -ne 'nosniff'){throw 'Falta la cabecera de seguridad.'}
Write-Host 'PASS: API, consumo HTTP desde MVC y cabeceras.'
