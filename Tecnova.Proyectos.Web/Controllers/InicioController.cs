using System.Net.Http;
using System.Threading.Tasks;
using System.Web.Mvc;
using Newtonsoft.Json.Linq;
using Tecnova.Proyectos.Web.Services;

namespace Tecnova.Proyectos.Web.Controllers
{
    public class InicioController : Controller
    {
        public async Task<ActionResult> Index()
        {
            try
            {
                var estado = JObject.Parse(await ApiClient.Estado());
                ViewBag.Conectado = (bool?)estado["disponible"] == true;
            }
            catch (HttpRequestException)
            {
                ViewBag.Conectado = false;
            }
            catch (TaskCanceledException)
            {
                ViewBag.Conectado = false;
            }
            catch (Newtonsoft.Json.JsonException)
            {
                ViewBag.Conectado = false;
            }
            return View();
        }
    }
}
