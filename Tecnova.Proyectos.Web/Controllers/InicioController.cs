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
            Response.Cache.SetCacheability(System.Web.HttpCacheability.NoCache); Response.Cache.SetNoStore();
            if (Session["ApiCookie"] == null) return RedirectToAction("Login", "Cuenta");
            var result = await ApiClient.Send("auth/me");
            if (result.Status == 401) return RedirectToAction("Login", "Cuenta");
            if (result.Status != 200) { ViewBag.Error = "No se pudo conectar con el servicio. Intente iniciar sesión nuevamente."; return View("~/Views/Cuenta/Login.cshtml"); }
            return View(JObject.Parse(result.Body));
        }
    }
}
