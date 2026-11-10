using System;
using System.Web;
using System.Web.Http;
using Newtonsoft.Json.Linq;
using Tecnova.Proyectos.Api.Data;
using Tecnova.Proyectos.Api.Security;

namespace Tecnova.Proyectos.Api.Controllers
{
    [RoutePrefix("api/auth")]
    public class AutenticacionController : BaseController
    {
        [AllowAnonymous, HttpPost, Route("login")]
        public IHttpActionResult Login(JObject input)
        {
            var email = Text(input, "Email", 180).ToLowerInvariant();
            var password = Text(input, "Contrasena", 128);
            using (var db = new Db(true))
            {
                var rows = db.Query("SELECT Id,Nombre,Email,IdRol,ContrasenaHash,SecurityStamp,Activo,BloqueadoHasta FROM Usuarios WITH(UPDLOCK) WHERE Email=@email", "@email", email);
                if (rows.Count == 0) { Passwords.Verify(password, "v1$600000$AAAAAAAAAAAAAAAAAAAAAA==$AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA="); Fail(401, "Correo o contraseña incorrectos."); }
                var user = rows[0];
                if (!(bool)user["Activo"] || (user["BloqueadoHasta"] != null && (DateTime)user["BloqueadoHasta"] > DateTime.UtcNow)) Fail(401, "No se pudo iniciar sesión. Revise sus credenciales o espere si hubo varios intentos.");
                if (!Passwords.Verify(password, (string)user["ContrasenaHash"]))
                {
                    db.Execute("UPDATE Usuarios SET Intentos=CASE WHEN Intentos>=4 THEN 0 ELSE Intentos+1 END,BloqueadoHasta=CASE WHEN Intentos>=4 THEN DATEADD(minute,15,SYSUTCDATETIME()) ELSE NULL END WHERE Id=@id", "@id", user["Id"]);
                    db.Commit(); Fail(401, "Correo o contraseña incorrectos.");
                }
                db.Execute("UPDATE Usuarios SET Intentos=0,BloqueadoHasta=NULL WHERE Id=@id", "@id", user["Id"]);
                db.Commit();
                HttpContext.Current.Session.Clear();
                HttpContext.Current.Session["IdUsuario"] = user["Id"];
                HttpContext.Current.Session["Stamp"] = user["SecurityStamp"].ToString();
                return Ok(new { Id = user["Id"], Nombre = user["Nombre"], Email = user["Email"], IdRol = user["IdRol"], Rol = (int)user["IdRol"] == 1 ? "Administrador" : "Usuario" });
            }
        }
        [HttpGet, Route("me")]
        public IHttpActionResult Me() { return Ok(new { Id = UserId, Nombre = Actor["Nombre"], Email = Actor["Email"], Rol = Actor["Rol"], IdRol = Actor["IdRol"] }); }
        [HttpPost, Route("logout")]
        public IHttpActionResult Logout() { HttpContext.Current.Session.Clear(); HttpContext.Current.Session.Abandon(); return Ok(new { mensaje = "Sesión cerrada." }); }
    }
}
