using System;
using System.Linq;
using System.Web.Http;
using Newtonsoft.Json.Linq;
using Tecnova.Proyectos.Api.Data;

namespace Tecnova.Proyectos.Api.Controllers
{
    [RoutePrefix("api/proyectos")]
    public class ProyectosController : BaseController
    {
        [HttpGet, Route("")]
        public IHttpActionResult List()
        {
            using (var db = new Db()) return Ok(db.Query(@"SELECT p.Id,p.Nombre,p.Descripcion,p.FechaInicio,p.FechaFinEstimada,p.IdEstado,e.Nombre Estado,p.IdResponsable,u.Nombre Responsable,p.Version,
                (SELECT COUNT(*) FROM Tareas t WHERE t.IdProyecto=p.Id) TotalTareas,
                (SELECT COUNT(*) FROM Tareas t WHERE t.IdProyecto=p.Id AND t.IdEstado=12) Completadas
                FROM Proyectos p JOIN Usuarios u ON u.Id=p.IdResponsable JOIN Estados e ON e.Id=p.IdEstado
                WHERE @admin=1 OR EXISTS(SELECT 1 FROM ProyectoMiembros m WHERE m.IdProyecto=p.Id AND m.IdUsuario=@u) ORDER BY p.FechaCreacion DESC,p.Id DESC", "@admin", IsAdmin, "@u", UserId));
        }

        [HttpGet, Route("{id:int}")]
        public IHttpActionResult Get(int id)
        {
            using (var db = new Db())
            {
                var project = Project(db, id);
                return Ok(new { Proyecto = project, Miembros = db.Query("SELECT u.Id,u.Nombre,u.IdRol,u.Activo FROM ProyectoMiembros m JOIN Usuarios u ON u.Id=m.IdUsuario WHERE m.IdProyecto=@id ORDER BY u.Nombre", "@id", id) });
            }
        }

        [HttpPost, Route("")]
        public IHttpActionResult Create(JObject input) { return Save(0, input); }
        [HttpPut, Route("{id:int}")]
        public IHttpActionResult Update(int id, JObject input) { return Save(id, input); }

        private IHttpActionResult Save(int id, JObject input)
        {
            RequireAdmin();
            var name = Text(input, "Nombre", 150); var description = Text(input, "Descripcion", 3000);
            var start = Date(input, "FechaInicio"); var end = Date(input, "FechaFinEstimada");
            if (end < start) Fail(400, "La fecha final debe ser igual o posterior al inicio.");
            var responsible = Number(input, "IdResponsable"); var state = Number(input, "IdEstado");
            if (state != 1 && state != 2) Fail(400, "Estado de proyecto inválido.");
            if (id == 0 && state != 1) Fail(400, "Un proyecto nuevo debe estar activo.");
            var array = input["Miembros"] as JArray;
            if (array == null || array.Count > 500 || array.Any(x => x.Type != JTokenType.Integer || (long)x <= 0 || (long)x > int.MaxValue)) Fail(400, "Seleccione los integrantes del proyecto.");
            var members = array.Select(x => (int)x).Concat(new[] { responsible }).Distinct().ToArray();
            using (var db = new Db(true))
            {
                if (id != 0) Project(db, id, true);
                var previous = id == 0 ? new int[0] : db.Query("SELECT IdUsuario FROM ProyectoMiembros WHERE IdProyecto=@id", "@id", id).Select(x => (int)x["IdUsuario"]).ToArray();
                if (db.Scalar("SELECT COUNT(*) FROM Usuarios WHERE Id=@id AND Activo=1 AND IdRol=1", "@id", responsible) == 0) Fail(400, "El responsable debe ser un administrador activo.");
                foreach (var member in members)
                    if (!previous.Contains(member) && db.Scalar("SELECT COUNT(*) FROM Usuarios WHERE Id=@id AND Activo=1", "@id", member) == 0) Fail(400, "Los nuevos participantes deben estar activos.");
                if (id == 0)
                    id = db.Scalar("INSERT Proyectos(Nombre,Descripcion,FechaInicio,FechaFinEstimada,IdEstado,IdResponsable) OUTPUT INSERTED.Id VALUES(@n,@d,@s,@e,@st,@r)", "@n", name, "@d", description, "@s", start, "@e", end, "@st", state, "@r", responsible);
                else
                {
                    if (state == 2 && db.Scalar("SELECT COUNT(*) FROM Tareas WHERE IdProyecto=@id AND IdEstado IN(10,11)", "@id", id) > 0) Fail(409, "Complete o cancele las tareas pendientes antes de cerrar el proyecto.");
                    if (db.Scalar("SELECT COUNT(*) FROM Tareas WHERE IdProyecto=@id AND (FechaVencimiento<@s OR FechaVencimiento>@e)", "@id", id, "@s", start, "@e", end) > 0) Fail(409, "El rango de fechas debe incluir el vencimiento de todas las tareas.");
                    if (db.Execute("UPDATE Proyectos SET Nombre=@n,Descripcion=@d,FechaInicio=@s,FechaFinEstimada=@e,IdEstado=@st,IdResponsable=@r,Version=Version+1 WHERE Id=@id AND Version=@v", "@n", name, "@d", description, "@s", start, "@e", end, "@st", state, "@r", responsible, "@id", id, "@v", Number(input, "Version")) == 0) Fail(409, "El proyecto cambió. Actualice antes de guardar.");
                }
                foreach (var removed in previous.Except(members))
                {
                    if (db.Scalar("SELECT COUNT(*) FROM Tareas WHERE IdProyecto=@p AND IdAsignado=@u", "@p", id, "@u", removed) > 0) Fail(409, "No puede retirar a un integrante con tareas asignadas. Reasigne primero sus tareas.");
                    db.Execute("DELETE ProyectoMiembros WHERE IdProyecto=@p AND IdUsuario=@u", "@p", id, "@u", removed);
                }
                foreach (var added in members.Except(previous)) db.Execute("INSERT ProyectoMiembros VALUES(@p,@u)", "@p", id, "@u", added);
                db.Commit(); return Ok(new { Id = id, mensaje = state == 2 ? "Proyecto cerrado." : "Proyecto guardado." });
            }
        }

        [HttpDelete, Route("{id:int}")]
        public IHttpActionResult Delete(int id, int version)
        {
            RequireAdmin();
            using (var db = new Db(true))
            {
                Project(db, id, true);
                if (db.Scalar("SELECT COUNT(*) FROM Tareas WHERE IdProyecto=@id", "@id", id) > 0) Fail(409, "Un proyecto con tareas debe conservarse. Puede cerrarlo cuando finalice el trabajo.");
                db.Execute("DELETE ProyectoMiembros WHERE IdProyecto=@id", "@id", id);
                if (db.Execute("DELETE Proyectos WHERE Id=@id AND Version=@v", "@id", id, "@v", version) == 0) Fail(409, "El proyecto cambió. Actualice la lista.");
                db.Commit(); return Ok(new { mensaje = "Proyecto eliminado." });
            }
        }
    }
}
