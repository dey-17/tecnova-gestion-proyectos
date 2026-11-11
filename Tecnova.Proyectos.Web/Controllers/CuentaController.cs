using System.Threading.Tasks;
using System.Web.Mvc;
using Newtonsoft.Json;
using Tecnova.Proyectos.Web.Services;

namespace Tecnova.Proyectos.Web.Controllers
{
    public class CuentaController : Controller
    {
        [HttpGet]
        public ActionResult Login() { Response.Cache.SetCacheability(System.Web.HttpCacheability.NoCache); Response.Cache.SetNoStore(); return View(); }
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<ActionResult> Entrar(string email, string contrasena)
        {
            var result = await ApiClient.Send("auth/login", "POST", JsonConvert.SerializeObject(new { Email = email, Contrasena = contrasena }));
            Response.StatusCode = result.Status; Response.TrySkipIisCustomErrors = true;
            return Content(result.Body, "application/json");
        }
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<ActionResult> Salir()
        {
            var result = await ApiClient.Send("auth/logout", "POST", "{}");
            if (result.Status == 503) { Response.StatusCode = 503; Response.TrySkipIisCustomErrors = true; return Content(result.Body, "application/json"); }
            Session.Clear(); Session.Abandon(); return Json(new { mensaje = "Sesión cerrada." });
        }
    }
}
