using System;
using System.Linq;
using System.Net.Mail;
using System.Web.Http;
using Newtonsoft.Json.Linq;
using Tecnova.Proyectos.Api.Data;
using Tecnova.Proyectos.Api.Security;

namespace Tecnova.Proyectos.Api.Controllers
{
    [RoutePrefix("api/usuarios")]
    public class UsuariosController : BaseController
    {
        private const string Columns = "u.Id,u.Nombre,u.Email,u.IdRol,r.Nombre Rol,u.Activo,u.Version,u.FechaRegistro";

        [HttpGet, Route("")]
        public IHttpActionResult List()
        {
            RequireAdmin();
            using (var db = new Db()) return Ok(db.Query("SELECT " + Columns + " FROM Usuarios u JOIN Roles r ON r.Id=u.IdRol ORDER BY u.Nombre"));
        }

        [HttpGet, Route("{id:int}")]
        public IHttpActionResult Get(int id)
        {
            RequireAdmin();
            using (var db = new Db())
            {
                var rows = db.Query("SELECT " + Columns + " FROM Usuarios u JOIN Roles r ON r.Id=u.IdRol WHERE u.Id=@id", "@id", id);
                if (rows.Count == 0) return NotFound();
                return Ok(rows[0]);
            }
        }

        [HttpPost, Route("")]
        public IHttpActionResult Create(JObject input) { return Save(0, input); }

        [HttpPut, Route("{id:int}")]
        public IHttpActionResult Update(int id, JObject input) { return Save(id, input); }

        private IHttpActionResult Save(int id, JObject input)
        {
            RequireAdmin();
            var name = Text(input, "Nombre", 120);
            var email = Text(input, "Email", 180).ToLowerInvariant();
            try { if (new MailAddress(email).Address != email) Fail(400, "El correo no es válido."); }
            catch (FormatException) { Fail(400, "El correo no es válido."); }
            var role = Number(input, "IdRol");
            if (role != 1 && role != 2) Fail(400, "Seleccione un rol válido.");
            var active = input["Activo"] == null || input["Activo"].Type != JTokenType.Boolean ? true : (bool)input["Activo"];
            var password = id == 0 || !string.IsNullOrEmpty((string)input["Contrasena"]) ? Passwords.Hash(Password(input)) : null;
            using (var db = new Db(true))
            {
                if (id == 0)
                {
                    id = db.Scalar("INSERT Usuarios(Nombre,Email,IdRol,ContrasenaHash,Activo) OUTPUT INSERTED.Id VALUES(@n,@e,@r,@p,@a)", "@n", name, "@e", email, "@r", role, "@p", password, "@a", active);
                }
                else
                {
                    if (id == UserId && (!active || role != 1)) Fail(409, "No puede quitarse su propio acceso de administrador.");
                    CheckAvailability(db, id, active, role);
                    var changed = db.Execute("UPDATE Usuarios SET Nombre=@n,Email=@e,IdRol=@r,Activo=@a,ContrasenaHash=COALESCE(@p,ContrasenaHash),SecurityStamp=CASE WHEN @p IS NOT NULL OR Activo<>@a OR IdRol<>@r THEN NEWID() ELSE SecurityStamp END,Version=Version+1,Intentos=CASE WHEN @p IS NOT NULL THEN 0 ELSE Intentos END,BloqueadoHasta=CASE WHEN @p IS NOT NULL THEN NULL ELSE BloqueadoHasta END WHERE Id=@id AND Version=@v", "@n", name, "@e", email, "@r", role, "@a", active, "@p", password, "@id", id, "@v", Number(input, "Version"));
                    if (changed == 0) Fail(409, "El usuario cambió. Actualice la lista antes de guardar.");
                }
                db.Commit();
                return Ok(new { Id = id, mensaje = "Usuario guardado." });
            }
        }

        private void CheckAvailability(Db db, int id, bool active, int role)
        {
            if ((!active || role != 1) && db.Scalar("SELECT COUNT(*) FROM Proyectos WHERE IdResponsable=@id AND IdEstado=1", "@id", id) > 0)
                Fail(409, "Primero cambie el responsable de sus proyectos activos.");
            if (!active && db.Scalar("SELECT COUNT(*) FROM Tareas WHERE IdAsignado=@id AND IdEstado IN(10,11)", "@id", id) > 0)
                Fail(409, "Primero reasigne o finalice las tareas pendientes del usuario.");
        }

        [HttpDelete, Route("{id:int}")]
        public IHttpActionResult Delete(int id, int version)
        {
            RequireAdmin();
            if (id == UserId) Fail(409, "No puede desactivar su propia cuenta.");
            using (var db = new Db(true))
            {
                CheckAvailability(db, id, false, 2);
                if (db.Execute("UPDATE Usuarios SET Activo=0,Version=Version+1,SecurityStamp=NEWID() WHERE Id=@id AND Version=@v", "@id", id, "@v", version) == 0) Fail(409, "El usuario cambió. Actualice la lista.");
                db.Commit(); return Ok(new { mensaje = "Usuario desactivado. Se conserva su historial." });
            }
        }
    }
}
