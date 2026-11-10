using System.Web.Http;
using Tecnova.Proyectos.Api.Data;

namespace Tecnova.Proyectos.Api.Controllers
{
    [RoutePrefix("api/catalogos")]
    public class CatalogosController : ApiController
    {
        [HttpGet, Route("")]
        public IHttpActionResult Get()
        {
            using (var db = new Db()) return Ok(new {
                Estados = db.Query("SELECT Id,Nombre,EntidadAplicacion FROM Estados ORDER BY Id"),
                Prioridades = db.Query("SELECT Id,Nombre FROM Prioridades ORDER BY Id"),
                Roles = db.Query("SELECT Id,Nombre FROM Roles ORDER BY Id")
            });
        }
    }
}
