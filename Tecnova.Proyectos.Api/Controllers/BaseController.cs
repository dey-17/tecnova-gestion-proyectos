using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Web;
using System.Web.Http;
using Newtonsoft.Json.Linq;

namespace Tecnova.Proyectos.Api.Controllers
{
    public abstract class BaseController : ApiController
    {
        protected Dictionary<string, object> Actor { get { return (Dictionary<string, object>)HttpContext.Current.Items["Actor"]; } }
        protected int UserId { get { return Convert.ToInt32(Actor["Id"]); } }
        protected bool IsAdmin { get { return Convert.ToInt32(Actor["IdRol"]) == 1; } }
        protected void RequireAdmin() { if (!IsAdmin) Fail(403, "Esta operación requiere un administrador."); }
        protected Dictionary<string, object> Project(Data.Db db, int id, bool writing = false)
        {
            var rows = db.Query("SELECT p.* FROM Proyectos p WHERE p.Id=@id AND (@admin=1 OR EXISTS(SELECT 1 FROM ProyectoMiembros m WHERE m.IdProyecto=p.Id AND m.IdUsuario=@u))", "@id", id, "@admin", IsAdmin, "@u", UserId);
            if (rows.Count == 0) Fail(404, "Proyecto no disponible.");
            if (writing && (int)rows[0]["IdEstado"] != 1) Fail(409, "El proyecto está cerrado y es de solo lectura.");
            return rows[0];
        }
        protected void Fail(int status, string message) { throw new HttpResponseException(Request.CreateResponse((HttpStatusCode)status, new { mensaje = message })); }
        protected string Text(JObject input, string key, int max, int min = 1)
        {
            var token = input == null ? null : input[key];
            var value = token != null && token.Type == JTokenType.String ? ((string)token).Trim() : "";
            if (value.Length < min || value.Length > max || value.IndexOfAny(new[] {'<','>'}) >= 0) Fail(400, key + ": revise la longitud y use texto sin etiquetas HTML.");
            return value;
        }
        protected int Number(JObject input, string key)
        {
            int value;
            if (input == null || !int.TryParse((string)input[key], out value) || value <= 0) { Fail(400, key + ": seleccione un valor válido."); return 0; }
            return value;
        }
        protected DateTime Date(JObject input, string key)
        {
            DateTime value;
            if (input == null || !DateTime.TryParseExact((string)input[key], "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out value)) { Fail(400, key + ": indique una fecha válida."); return DateTime.MinValue; }
            return value;
        }
        protected string Password(JObject input)
        {
            var value = Text(input, "Contrasena", 128, 12);
            if (!System.Text.RegularExpressions.Regex.IsMatch(value, "[a-z]") || !System.Text.RegularExpressions.Regex.IsMatch(value, "[A-Z]") || !System.Text.RegularExpressions.Regex.IsMatch(value, "[0-9]") || !System.Text.RegularExpressions.Regex.IsMatch(value, "[^a-zA-Z0-9]")) Fail(400, "La contraseña requiere 12 caracteres, mayúscula, minúscula, número y símbolo.");
            return value;
        }
    }
}
