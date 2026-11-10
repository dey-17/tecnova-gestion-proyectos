using System.Web.Http;

namespace Tecnova.Proyectos.Api.Controllers
{
    [RoutePrefix("api/estado")]
    public class EstadoController : ApiController
    {
        [HttpGet, Route("")]
        public IHttpActionResult Get()
        {
            return Ok(new
            {
                servicio = "Tecnova Gestión de Proyectos",
                disponible = true
            });
        }
    }
}
