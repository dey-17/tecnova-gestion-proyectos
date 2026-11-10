using System.Web;
using System.Web.Http;

namespace Tecnova.Proyectos.Api
{
    public class Application : HttpApplication
    {
        protected void Application_PostAuthorizeRequest()
        {
            HttpContext.Current.SetSessionStateBehavior(System.Web.SessionState.SessionStateBehavior.Required);
        }

        protected void Application_Start()
        {
            GlobalConfiguration.Configure(config =>
            {
                config.MapHttpAttributeRoutes();
                config.Filters.Add(new Security.ApiSecurity());
                config.Filters.Add(new Security.SafeErrors());
                config.Formatters.Remove(config.Formatters.XmlFormatter);
            });
        }
    }
}
