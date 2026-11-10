using System.Web.Http;

namespace Tecnova.Proyectos.Api.Controllers
{
    [RoutePrefix("api/estado")]
    public class EstadoController : ApiController
    {
        [AllowAnonymous, HttpGet, Route("")]
        public IHttpActionResult Get()
        {
            return Ok(new
            {
                servicio = "Tecnova GestiÃ³n de Proyectos",
                disponible = true
            });
        }
    }
}
