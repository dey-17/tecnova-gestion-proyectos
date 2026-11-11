using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web.Mvc;
using Tecnova.Proyectos.Web.Services;

namespace Tecnova.Proyectos.Web.Controllers
{
    public class ServicioController : Controller
    {
        private static bool Valid(string resource)
        {
            return resource != null && Regex.IsMatch(resource, @"\A(?:catalogos|indicadores|usuarios(?:/\d+)?|proyectos(?:/\d+(?:/historial)?)?|tareas(?:/\d+(?:/historial)?)?)(?:\?(?:version|proyecto)=\d+)?\z");
        }
        private async Task<ActionResult> Relay(string recurso, string method, string payload)
        {
            Response.Cache.SetCacheability(System.Web.HttpCacheability.NoCache); Response.Cache.SetNoStore();
            if (Session["ApiCookie"] == null) { Response.StatusCode = 401; Response.TrySkipIisCustomErrors = true; return Json(new { mensaje = "Su sesión terminó. Inicie sesión nuevamente." }, JsonRequestBehavior.AllowGet); }
            if (!Valid(recurso)) return new HttpStatusCodeResult(400);
            var result = await ApiClient.Send(recurso, method, payload);
            Response.StatusCode = result.Status; Response.TrySkipIisCustomErrors = true;
            return Content(result.Body, "application/json");
        }
        [HttpGet]
        public Task<ActionResult> Consultar(string recurso) { return Relay(recurso, "GET", null); }
        [HttpPost, ValidateAntiForgeryToken]
        public Task<ActionResult> Enviar(string recurso, string metodo, string datos)
        {
            if (metodo != "POST" && metodo != "PUT" && metodo != "DELETE") return Task.FromResult<ActionResult>(new HttpStatusCodeResult(405));
            return Relay(recurso, metodo, datos);
        }
    }
}
