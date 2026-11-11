#requires -Version 7.0
param([string]$Api='http://localhost:51851',[string]$Web='http://localhost:51850',[string]$Servidor='(localdb)\MSSQLLocalDB')
$ErrorActionPreference='Stop'
$checks=0
$run=[guid]::NewGuid().ToString('N')
$prefix="qa.$run"
$admin=New-Object Microsoft.PowerShell.Commands.WebRequestSession
$member=New-Object Microsoft.PowerShell.Commands.WebRequestSession
$outsider=New-Object Microsoft.PowerShell.Commands.WebRequestSession
$anonymous=New-Object Microsoft.PowerShell.Commands.WebRequestSession
$projectIds=New-Object System.Collections.Generic.List[int]
$userIds=New-Object System.Collections.Generic.List[int]
function Call($session,$method,$path,$body=$null,[int]$expected=200){
 $args=@{Uri="$Api/api/$path";Method=$method;WebSession=$session;Headers=@{'X-Tecnova-Client'='tests'};SkipHttpErrorCheck=$true}
 if($null -ne $body){$args.ContentType='application/json';$args.Body=[Text.Encoding]::UTF8.GetBytes(($body|ConvertTo-Json -Depth 8))}
 $response=Invoke-WebRequest @args
 if([int]$response.StatusCode -ne $expected){throw "$method $path : se esperaba $expected, se recibió $($response.StatusCode): $($response.Content)"}
 $script:checks++
 if($response.Content){return $response.Content|ConvertFrom-Json}
}
function Assert($condition,$message){if(!$condition){throw $message};$script:checks++}
try {
 $null=Call $anonymous GET usuarios $null 401
 $null=Call $admin POST auth/login @{Email='admin.demo@tecnova.local';Contrasena='Incorrecta!2026'} 401
 $a=Call $admin POST auth/login @{Email='admin.demo@tecnova.local';Contrasena='Tecnova.Admin!2026'}
 Assert ($a.IdRol -eq 1) 'El administrador no tiene el rol correcto.'
 foreach($name in @('miembro','externo')){
  $u=Call $admin POST usuarios @{Nombre="QA $run $name";Email="$prefix.$name@example.invalid";Contrasena='Prueba.Temporal!2026';IdRol=2;Activo=$true}
  $userIds.Add([int]$u.Id)
 }
 $uid=$userIds[0];$outsideId=$userIds[1]
 $null=Call $admin POST usuarios @{Nombre='Duplicado';Email="$prefix.miembro@example.invalid";Contrasena='Prueba.Temporal!2026';IdRol=2} 409
 $null=Call $admin POST usuarios @{Nombre='Contraseña débil';Email="$prefix.weak@example.invalid";Contrasena='123';IdRol=2} 400
 $null=Call $admin POST usuarios @{Nombre='Rol inválido';Email="$prefix.role@example.invalid";Contrasena='Prueba.Temporal!2026';IdRol=99} 400
 $null=Call $member POST auth/login @{Email="$prefix.miembro@example.invalid";Contrasena='Prueba.Temporal!2026'}
 $null=Call $outsider POST auth/login @{Email="$prefix.externo@example.invalid";Contrasena='Prueba.Temporal!2026'}
 $null=Call $member GET usuarios $null 403
 $null=Call $member POST usuarios @{Nombre='No permitido'} 403
 $start=[DateTime]::UtcNow.Date.ToString('yyyy-MM-dd');$end=[DateTime]::UtcNow.Date.AddDays(30).ToString('yyyy-MM-dd');$due=[DateTime]::UtcNow.Date.AddDays(10).ToString('yyyy-MM-dd')
 $project=@{Nombre="QA $run";Descripcion='Proyecto temporal de pruebas';FechaInicio=$start;FechaFinEstimada=$end;IdEstado=1;IdResponsable=$a.Id;Miembros=@($uid)}
 $null=Call $member POST proyectos $project 403
 $p=Call $admin POST proyectos $project;$pidTest=[int]$p.Id;$projectIds.Add($pidTest)
 $project.Version=1
 $detail=Call $member GET "proyectos/$pidTest"
 Assert ($detail.Miembros.Count -eq 2) 'No se incluyó al responsable o al integrante.'
 $null=Call $outsider GET "proyectos/$pidTest" $null 404
 $null=Call $outsider GET "tareas?proyecto=$pidTest" $null 404
 $list=Call $outsider GET proyectos
 Assert (@($list|Where-Object Id -eq $pidTest).Count -eq 0) 'Se expuso un proyecto a alguien ajeno.'
 $t=@{IdProyecto=$pidTest;Titulo='Tarea de prueba';Descripcion='Descripción válida';IdAsignado=$uid;IdEstado=10;IdPrioridad=2;FechaVencimiento=$due}
 $bad=$t.Clone();$bad.IdAsignado=$outsideId
 $null=Call $admin POST tareas $bad 400
 $bad=$t.Clone();$bad.IdEstado=@{valor=10}
 $null=Call $admin POST tareas $bad 400
 $bad=$t.Clone();$bad.IdEstado=1
 $null=Call $admin POST tareas $bad 400
 $bad=$t.Clone();$bad.FechaVencimiento='1900-01-01'
 $null=Call $admin POST tareas $bad 400
 $bad=$t.Clone();$bad.Titulo='<script>alert(1)</script>'
 $null=Call $member POST tareas $bad 400
 $created=Call $member POST tareas $t;$tid=[int]$created.Id;$t.Version=1
 $history=Call $member GET "tareas/$tid/historial"
 Assert (@($history).Count -eq 1 -and $history[0].Accion -eq 'Tarea creada' -and $history[0].Usuario -like "QA $run*") 'No se registró autor y creación de la tarea.'
 $null=Call $outsider GET "tareas/$tid/historial" $null 404
 $null=Call $outsider GET "tareas/$tid" $null 404
 $null=Call $outsider PUT "tareas/$tid" $t 404
 $t.IdEstado=11;$t.IdPrioridad=1
 $null=Call $member PUT "tareas/$tid" $t
 $null=Call $member PUT "tareas/$tid" $t 409
 $history=Call $member GET "tareas/$tid/historial"
 Assert (@($history).Count -eq 2 -and $history[0].Detalle -match 'Pendiente → En progreso' -and $history[0].Detalle -match 'Media → Alta' -and (($history[0].Fecha -is [datetime] -and $history[0].Fecha.Kind -eq [DateTimeKind]::Utc) -or $history[0].Fecha -match 'Z$')) 'El historial no conserva cambios de estado/prioridad y fecha UTC, o registró un cambio rechazado.'
 $t.Version=2
 $other=$t.Clone();$other.Remove('Version');$other.IdAsignado=$a.Id;$other.IdEstado=10
 $otherCreated=Call $admin POST tareas $other;$otherId=[int]$otherCreated.Id;$other.Version=1
 $null=Call $member PUT "tareas/$otherId" $other 403
 $null=Call $member DELETE "tareas/$otherId`?version=1" $null 403
 $k=Call $member GET indicadores
 Assert ($k.ProyectosActivos -eq 1 -and $k.MisTareas -eq 1 -and ($k.Estados|Measure-Object Cantidad -Sum).Sum -eq 2) 'Los indicadores no respetan el alcance.'
 $closed=$project.Clone();$closed.IdEstado=2
 $null=Call $admin PUT "proyectos/$pidTest" $closed 409
 $null=Call $admin DELETE "usuarios/$uid`?version=1" $null 409
 $removed=$project.Clone();$removed.Miembros=@()
 $null=Call $admin PUT "proyectos/$pidTest" $removed 409
 $t.IdEstado=12
 $null=Call $member PUT "tareas/$tid" $t
 $other.IdEstado=13
 $null=Call $admin PUT "tareas/$otherId" $other
 $null=Call $admin DELETE "usuarios/$uid`?version=1"
 $null=Call $member GET auth/me $null 401
 $null=Call $admin PUT "proyectos/$pidTest" $closed
 $t.Version=3
 $null=Call $admin PUT "tareas/$tid" $t 409
 $null=Call $admin PUT "usuarios/$uid" @{Nombre="QA $run miembro";Email="$prefix.miembro@example.invalid";IdRol=2;Activo=$true;Version=2}
 $null=Call $member GET auth/me $null 401
 $null=Call $member POST auth/login @{Email="$prefix.miembro@example.invalid";Contrasena='Prueba.Temporal!2026'}
 $null=Call $admin DELETE "proyectos/$pidTest`?version=2" $null 409
 $temp=$project.Clone();$temp.Nombre="QA $run vacío"
 $empty=Call $admin POST proyectos $temp;$emptyId=[int]$empty.Id;$projectIds.Add($emptyId)
 $removable=$t.Clone();$removable.IdProyecto=$emptyId;$removable.IdEstado=10
 $extra=Call $admin POST tareas $removable
 $null=Call $admin DELETE "tareas/$($extra.Id)?version=1"
 $history=Call $admin GET "proyectos/$emptyId/historial"
 Assert (@($history|Where-Object Accion -eq 'Tarea eliminada').Count -eq 1) 'Se perdió el evento de eliminación de la tarea.'
 $null=Call $admin DELETE "proyectos/$emptyId`?version=1"
 $update=@{Nombre="QA $run editado";Email="$prefix.externo@example.invalid";IdRol=2;Activo=$true;Version=1}
 $null=Call $admin PUT "usuarios/$outsideId" $update
 $null=Call $admin PUT "usuarios/$outsideId" $update 409
 $null=Call $admin DELETE "usuarios/$outsideId`?version=2"
 $null=Call $outsider GET auth/me $null 401
 $null=Call $outsider POST auth/login @{Email="$prefix.externo@example.invalid";Contrasena='Prueba.Temporal!2026'} 401
 $null=Call $member POST auth/logout @{}
 $null=Call $member GET auth/me $null 401
 # La capa MVC debe autenticar, consumir la API y exigir antifalsificación.
 $mvc=New-Object Microsoft.PowerShell.Commands.WebRequestSession
 $login=Invoke-WebRequest "$Web/Cuenta/Login" -WebSession $mvc
 $token=[regex]::Match($login.Content,'name="__RequestVerificationToken"[^>]*value="([^"]+)"').Groups[1].Value
 Assert (![string]::IsNullOrWhiteSpace($token)) 'Falta token antifalsificación MVC.'
 $response=Invoke-WebRequest "$Web/Cuenta/Entrar" -Method Post -WebSession $mvc -Body @{email='admin.demo@tecnova.local';contrasena='Tecnova.Admin!2026';__RequestVerificationToken=$token} -SkipHttpErrorCheck
 Assert ($response.StatusCode -eq 200) 'No funciona login por MVC.'
 $page=Invoke-WebRequest "$Web/" -WebSession $mvc
 Assert ($page.Content -match 'data-role="1"') 'MVC no mostró el panel autorizado.'
 $res=Invoke-WebRequest "$Web/Servicio/Consultar?recurso=indicadores" -WebSession $mvc
 Assert ($res.StatusCode -eq 200 -and $res.Content -match 'ProyectosActivos') 'MVC no consume indicadores de la API.'
 $forbidden=Invoke-WebRequest "$Web/Servicio/Enviar" -Method Post -WebSession $mvc -Body @{recurso='usuarios';metodo='POST';datos='{}'} -SkipHttpErrorCheck
 Assert ($forbidden.StatusCode -eq 400 -and $forbidden.Content -match 'mensaje') 'Se aceptó POST sin antifalsificación o no se devolvió un error controlado.'
 $invalid=Invoke-WebRequest "$Web/Servicio/Consultar?recurso=https%3A%2F%2Fexample.com" -WebSession $mvc -SkipHttpErrorCheck
 Assert ($invalid.StatusCode -eq 400) 'El cliente MVC permite destinos arbitrarios.'
 Write-Host "PASS: $checks comprobaciones de autenticación, CRUD, roles, integridad, concurrencia, indicadores y MVC."
} finally {
 # Retirar exclusivamente las entidades identificadas de esta ejecución.
 Add-Type -AssemblyName System.Data
 $builder=New-Object System.Data.SqlClient.SqlConnectionStringBuilder
 $builder['Data Source']=$Servidor;$builder['Initial Catalog']='TecnovaProyectos';$builder['Integrated Security']=$true
 $connection=New-Object System.Data.SqlClient.SqlConnection($builder.ConnectionString)
 try{
  $connection.Open();$tx=$connection.BeginTransaction()
  try{
   foreach($id in $projectIds){
    $cmd=$connection.CreateCommand();$cmd.Transaction=$tx
    $cmd.CommandText='DELETE HistorialCambios WHERE IdProyecto=@id; IF EXISTS(SELECT 1 FROM Proyectos WHERE Id=@id AND Nombre LIKE @prefix) BEGIN DELETE Tareas WHERE IdProyecto=@id; DELETE ProyectoMiembros WHERE IdProyecto=@id; DELETE Proyectos WHERE Id=@id; END'
    [void]$cmd.Parameters.AddWithValue('@id',$id);[void]$cmd.Parameters.AddWithValue('@prefix',"QA $run%");[void]$cmd.ExecuteNonQuery();$cmd.Dispose()
   }
   foreach($id in $userIds){
    $cmd=$connection.CreateCommand();$cmd.Transaction=$tx;$cmd.CommandText='DELETE Usuarios WHERE Id=@id AND Email LIKE @prefix'
    [void]$cmd.Parameters.AddWithValue('@id',$id);[void]$cmd.Parameters.AddWithValue('@prefix',"$prefix.%@example.invalid");[void]$cmd.ExecuteNonQuery();$cmd.Dispose()
   }
   $tx.Commit()
  }catch{$tx.Rollback();throw}finally{$tx.Dispose()}
 }finally{$connection.Dispose()}
}
