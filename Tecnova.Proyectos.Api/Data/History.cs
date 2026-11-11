using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Tecnova.Proyectos.Api.Data
{
    public static class History
    {
        public static Dictionary<string, object> TaskSnapshot(Db db, int id)
        {
            return db.Query(@"SELECT t.Titulo [Título],t.Descripcion [Descripción],u.Nombre [Responsable],
                e.Nombre [Estado],p.Nombre [Prioridad],t.FechaVencimiento [Vencimiento]
                FROM Tareas t JOIN Usuarios u ON u.Id=t.IdAsignado JOIN Estados e ON e.Id=t.IdEstado
                JOIN Prioridades p ON p.Id=t.IdPrioridad WHERE t.Id=@id", "@id", id).FirstOrDefault();
        }

        public static Dictionary<string, object> ProjectSnapshot(Db db, int id)
        {
            var row = db.Query(@"SELECT p.Nombre,p.Descripcion [Descripción],p.FechaInicio [Inicio],
                p.FechaFinEstimada [Fin estimado],u.Nombre [Responsable],e.Nombre [Estado]
                FROM Proyectos p JOIN Usuarios u ON u.Id=p.IdResponsable JOIN Estados e ON e.Id=p.IdEstado
                WHERE p.Id=@id", "@id", id).FirstOrDefault();
            if (row != null) row["Integrantes"] = string.Join(", ", db.Query("SELECT u.Nombre FROM ProyectoMiembros m JOIN Usuarios u ON u.Id=m.IdUsuario WHERE m.IdProyecto=@id ORDER BY u.Nombre,u.Id", "@id", id).Select(x => (string)x["Nombre"]));
            return row;
        }

        private static string Display(object value)
        {
            if (value is DateTime) return ((DateTime)value).ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);
            return Convert.ToString(value, CultureInfo.InvariantCulture);
        }

        public static void Record(Db db, int projectId, int? taskId, int userId, string userName,
            string action, Dictionary<string, object> before, Dictionary<string, object> after)
        {
            var changes = new List<string>();
            foreach (var pair in after ?? before)
            {
                object oldValue = null;
                if (before != null) before.TryGetValue(pair.Key, out oldValue);
                var previous = Display(oldValue);
                var current = Display(pair.Value);
                if (before == null || after == null) changes.Add(pair.Key + ": " + current);
                else if (previous != current) changes.Add(pair.Key + ": " + previous + " → " + current);
            }
            if (changes.Count == 0) return;
            db.Execute("INSERT HistorialCambios(IdProyecto,IdTarea,IdUsuario,Usuario,Accion,Detalle) VALUES(@p,@t,@u,@n,@a,@d)",
                "@p", projectId, "@t", taskId, "@u", userId, "@n", userName, "@a", action, "@d", string.Join("\n", changes));
        }
    }
}
