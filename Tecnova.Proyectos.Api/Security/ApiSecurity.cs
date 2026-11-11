using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web;
using System.Web.Http;
using System.Web.Http.Controllers;
using System.Web.Http.Filters;
using Tecnova.Proyectos.Api.Data;

namespace Tecnova.Proyectos.Api.Security
{
    public class ApiSecurity : ActionFilterAttribute
    {
        public override void OnActionExecuting(HttpActionContext action)
        {
            var verb = action.Request.Method.Method;
            if (verb != "GET" && !action.Request.Headers.Contains("X-Tecnova-Client"))
            { action.Response = action.Request.CreateResponse(HttpStatusCode.Forbidden, new { mensaje = "Solicitud no permitida." }); return; }
            if (action.ActionDescriptor.GetCustomAttributes<AllowAnonymousAttribute>().Any()) return;
            var context = HttpContext.Current;
            if (context.Session["IdUsuario"] == null) { Deny(action); return; }
            using (var db = new Db())
            {
                var rows = db.Query("SELECT u.Id,u.Nombre,u.Email,u.IdRol,r.Nombre Rol,u.SecurityStamp FROM Usuarios u JOIN Roles r ON r.Id=u.IdRol WHERE u.Id=@id AND u.Activo=1", "@id", context.Session["IdUsuario"]);
                if (rows.Count == 0 || rows[0]["SecurityStamp"].ToString() != (string)context.Session["Stamp"]) { context.Session.Clear(); Deny(action); return; }
                context.Items["Actor"] = rows[0];
            }
        }
        private static void Deny(HttpActionContext a) { a.Response = a.Request.CreateResponse(HttpStatusCode.Unauthorized, new { mensaje = "Inicie sesión para continuar." }); }
    }
    public class SafeErrors : ExceptionFilterAttribute
    {
        public override void OnException(HttpActionExecutedContext context)
        {
            var sql = context.Exception as System.Data.SqlClient.SqlException;
            var status = sql != null && (sql.Number == 2601 || sql.Number == 2627 || sql.Number == 547 || sql.Number == 1205) ? HttpStatusCode.Conflict : HttpStatusCode.InternalServerError;
            if (context.Exception is Newtonsoft.Json.JsonException || context.Exception is ArgumentException || context.Exception is InvalidCastException || context.Exception is OverflowException)
                status = HttpStatusCode.BadRequest;
            var id = Guid.NewGuid().ToString("N");
            System.Diagnostics.Trace.TraceError("Tecnova {0}: {1}", id, context.Exception);
            context.Response = context.Request.CreateResponse(status, new { mensaje = status == HttpStatusCode.BadRequest ? "Revise el formato y los valores de los datos enviados." : status == HttpStatusCode.Conflict ? "Los datos cambiaron o tienen registros relacionados. Actualice la lista y revise la operación." : "No se pudo completar la operación. Intente nuevamente. Referencia: " + id });
        }
    }
}
