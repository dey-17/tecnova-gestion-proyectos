param([string]$Web='http://localhost:51850',[string]$Api='http://localhost:51851')
$ErrorActionPreference='Stop'
$status=Invoke-RestMethod "$Api/api/estado"
if($status.disponible -ne $true){throw 'La API no respondió correctamente.'}
$page=Invoke-WebRequest "$Web/" -UseBasicParsing
if($page.Content -notmatch 'Iniciar sesión'){throw 'MVC no mostró el login para una visita sin sesión.'}
if($page.Headers['X-Content-Type-Options'] -ne 'nosniff'){throw 'Falta la cabecera de seguridad.'}
Write-Host 'PASS: disponibilidad de API, página de login MVC y cabeceras.'
