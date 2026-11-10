using System.Web;
using System.Web.Http;

namespace Tecnova.Proyectos.Api
{
    public class Application : HttpApplication
    {
        protected void Application_Start()
        {
            GlobalConfiguration.Configure(config =>
            {
                config.MapHttpAttributeRoutes();
                config.Formatters.Remove(config.Formatters.XmlFormatter);
            });
        }
    }
}
