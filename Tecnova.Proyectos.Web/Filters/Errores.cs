using System;
using System.Web.Mvc;

namespace Tecnova.Proyectos.Web.Filters
{
    public class Errores : HandleErrorAttribute
    {
        public override void OnException(ExceptionContext context)
        {
            if (context.ExceptionHandled) return;
            bool invalid = context.Exception is HttpAntiForgeryException;
            string reference = Guid.NewGuid().ToString("N");
            System.Diagnostics.Trace.TraceError("Tecnova MVC {0}: {1}", reference, context.Exception);
            context.HttpContext.Response.StatusCode = invalid ? 400 : 500;
            context.HttpContext.Response.TrySkipIisCustomErrors = true;
            context.Result = new JsonResult
            {
                Data = new { mensaje = invalid ? "El formulario venció o no es válido. Recargue la página e intente nuevamente." : "No se pudo completar la operación. Referencia: " + reference },
                JsonRequestBehavior = JsonRequestBehavior.AllowGet
            };
            context.ExceptionHandled = true;
        }
    }
}
