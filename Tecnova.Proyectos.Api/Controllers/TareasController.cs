using System;
using System.Web.Http;
using Newtonsoft.Json.Linq;
using Tecnova.Proyectos.Api.Data;

namespace Tecnova.Proyectos.Api.Controllers
{
    [RoutePrefix("api/tareas")]
    public class TareasController : BaseController
    {
        private const string Select = @"SELECT t.Id,t.IdProyecto,p.Nombre Proyecto,t.Titulo,t.Descripcion,t.IdAsignado,u.Nombre Asignado,t.IdEstado,e.Nombre Estado,t.IdPrioridad,pr.Nombre Prioridad,t.FechaCreacion,t.FechaVencimiento,t.Version,p.IdEstado EstadoProyecto
            FROM Tareas t JOIN Proyectos p ON p.Id=t.IdProyecto JOIN Usuarios u ON u.Id=t.IdAsignado JOIN Estados e ON e.Id=t.IdEstado JOIN Prioridades pr ON pr.Id=t.IdPrioridad ";

        [HttpGet, Route("")]
        public IHttpActionResult List(int? proyecto = null)
        {
            using (var db = new Db())
            {
                if (proyecto.HasValue)
                {
                    Project(db, proyecto.Value);
                    return Ok(db.Query(Select + "WHERE t.IdProyecto=@p ORDER BY t.IdEstado,t.FechaVencimiento,t.Id", "@p", proyecto.Value));
                }
                return Ok(db.Query(Select + "WHERE @admin=1 OR (t.IdAsignado=@u AND EXISTS(SELECT 1 FROM ProyectoMiembros m WHERE m.IdProyecto=t.IdProyecto AND m.IdUsuario=@u)) ORDER BY t.IdEstado,t.FechaVencimiento,t.Id", "@admin", IsAdmin, "@u", UserId));
            }
        }
        [HttpGet, Route("{id:int}")]
        public IHttpActionResult Get(int id)
        {
            using (var db = new Db())
            {
                var rows = db.Query(Select + "WHERE t.Id=@id", "@id", id);
                if (rows.Count == 0) return NotFound();
                Project(db, (int)rows[0]["IdProyecto"]);
                return Ok(rows[0]);
            }
        }
        [HttpPost, Route("")]
        public IHttpActionResult Create(JObject input) { return Save(0, input); }
        [HttpPut, Route("{id:int}")]
        public IHttpActionResult Update(int id, JObject input) { return Save(id, input); }

        private IHttpActionResult Save(int id, JObject input)
        {
            var projectId = Number(input, "IdProyecto"); var assigned = Number(input, "IdAsignado");
            var title = Text(input, "Titulo", 150); var description = Text(input, "Descripcion", 3000);
            var state = Number(input, "IdEstado"); var priority = Number(input, "IdPrioridad"); var due = Date(input, "FechaVencimiento");
            if (state < 10 || state > 13 || priority < 1 || priority > 3) Fail(400, "Estado o prioridad no válidos.");
            using (var db = new Db(true))
            {
                var project = Project(db, projectId, true);
                if (due < (DateTime)project["FechaInicio"] || due > (DateTime)project["FechaFinEstimada"]) Fail(400, "El vencimiento debe estar dentro de las fechas del proyecto.");
                if (db.Scalar("SELECT COUNT(*) FROM ProyectoMiembros m JOIN Usuarios u ON u.Id=m.IdUsuario WHERE m.IdProyecto=@p AND m.IdUsuario=@u AND u.Activo=1", "@p", projectId, "@u", assigned) == 0) Fail(400, "Seleccione un integrante activo del proyecto.");
                if (id == 0)
                    id = db.Scalar("INSERT Tareas(IdProyecto,Titulo,Descripcion,IdAsignado,IdEstado,IdPrioridad,FechaVencimiento) OUTPUT INSERTED.Id VALUES(@p,@t,@d,@a,@s,@pr,@f)", "@p", projectId, "@t", title, "@d", description, "@a", assigned, "@s", state, "@pr", priority, "@f", due);
                else
                {
                    var current = db.Query("SELECT IdAsignado,IdProyecto FROM Tareas WHERE Id=@id", "@id", id);
                    if (current.Count == 0) return NotFound();
                    if ((int)current[0]["IdProyecto"] != projectId) Fail(400, "No se puede cambiar el proyecto de una tarea existente.");
                    if (!IsAdmin && (int)current[0]["IdAsignado"] != UserId) Fail(403, "Solo puede modificar las tareas que tiene asignadas.");
                    if (db.Execute("UPDATE Tareas SET Titulo=@t,Descripcion=@d,IdAsignado=@a,IdEstado=@s,IdPrioridad=@pr,FechaVencimiento=@f,Version=Version+1 WHERE Id=@id AND Version=@v", "@t", title, "@d", description, "@a", assigned, "@s", state, "@pr", priority, "@f", due, "@id", id, "@v", Number(input, "Version")) == 0) Fail(409, "La tarea cambió. Actualice antes de guardar.");
                }
                db.Commit(); return Ok(new { Id = id, mensaje = "Tarea guardada." });
            }
        }
        [HttpDelete, Route("{id:int}")]
        public IHttpActionResult Delete(int id, int version)
        {
            using (var db = new Db(true))
            {
                var rows = db.Query("SELECT IdProyecto,IdAsignado,IdEstado FROM Tareas WHERE Id=@id", "@id", id);
                if (rows.Count == 0) return NotFound();
                Project(db, (int)rows[0]["IdProyecto"], true);
                if (!IsAdmin && (int)rows[0]["IdAsignado"] != UserId) Fail(403, "Solo puede eliminar tareas que tenga asignadas.");
                if ((int)rows[0]["IdEstado"] != 10) Fail(409, "Solo se eliminan tareas pendientes. Para conservar el trabajo realizado, use Cancelada.");
                if (db.Execute("DELETE Tareas WHERE Id=@id AND Version=@v", "@id", id, "@v", version) == 0) Fail(409, "La tarea cambió. Actualice la lista.");
                db.Commit(); return Ok(new { mensaje = "Tarea eliminada." });
            }
        }
    }
}
