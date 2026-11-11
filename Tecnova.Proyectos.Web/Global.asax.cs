using System.Web;
using System.Web.Mvc;
using System.Web.Routing;

namespace Tecnova.Proyectos.Web
{
    public class Application : HttpApplication
    {
        protected void Application_Start()
        {
            GlobalFilters.Filters.Add(new Filters.Errores());
            RouteTable.Routes.IgnoreRoute("{resource}.axd/{*pathInfo}");
            RouteTable.Routes.MapRoute(
                "Default", "{controller}/{action}/{id}",
                new { controller = "Inicio", action = "Index", id = UrlParameter.Optional });
        }
    }
}
