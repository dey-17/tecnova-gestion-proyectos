using System.Web.Http;
using Tecnova.Proyectos.Api.Data;

namespace Tecnova.Proyectos.Api.Controllers
{
    [RoutePrefix("api/indicadores")]
    public class IndicadoresController : BaseController
    {
        [HttpGet, Route("")]
        public IHttpActionResult Get()
        {
            const string scope = "(@admin=1 OR EXISTS(SELECT 1 FROM ProyectoMiembros m WHERE m.IdProyecto=p.Id AND m.IdUsuario=@u))";
            using (var db = new Db(true))
            {
                var active = db.Scalar("SELECT COUNT(*) FROM Proyectos p WHERE p.IdEstado=1 AND " + scope, "@admin", IsAdmin, "@u", UserId);
                var mine = db.Scalar("SELECT COUNT(*) FROM Tareas t JOIN Proyectos p ON p.Id=t.IdProyecto WHERE t.IdAsignado=@u AND " + scope, "@admin", IsAdmin, "@u", UserId);
                var overdue = db.Scalar("SELECT COUNT(*) FROM Tareas t JOIN Proyectos p ON p.Id=t.IdProyecto WHERE t.IdEstado IN(10,11) AND t.FechaVencimiento<CAST(SYSUTCDATETIME() AS date) AND " + scope, "@admin", IsAdmin, "@u", UserId);
                var states = db.Query("SELECT e.Id,e.Nombre,COUNT(t.Id) Cantidad FROM Estados e LEFT JOIN (SELECT t.Id,t.IdEstado FROM Tareas t JOIN Proyectos p ON p.Id=t.IdProyecto WHERE " + scope + ") t ON t.IdEstado=e.Id WHERE e.EntidadAplicacion='Tarea' GROUP BY e.Id,e.Nombre ORDER BY e.Id", "@admin", IsAdmin, "@u", UserId);
                db.Commit();
                return Ok(new { ProyectosActivos = active, MisTareas = mine, TareasVencidas = overdue, Estados = states });
            }
        }
    }
}
