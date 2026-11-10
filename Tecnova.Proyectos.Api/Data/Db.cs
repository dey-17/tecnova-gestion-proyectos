using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;

namespace Tecnova.Proyectos.Api.Data
{
    public sealed class Db : IDisposable
    {
        private readonly SqlConnection connection;
        private SqlTransaction transaction;
        public Db(bool transactional = false)
        {
            connection = new SqlConnection(ConfigurationManager.ConnectionStrings["TecnovaProyectos"].ConnectionString);
            connection.Open();
            if (transactional) transaction = connection.BeginTransaction(IsolationLevel.Serializable);
        }
        private SqlCommand Command(string sql, object[] pairs)
        {
            var command = new SqlCommand(sql, connection, transaction);
            command.CommandTimeout = 20;
            for (int i = 0; i < pairs.Length; i += 2)
                command.Parameters.AddWithValue((string)pairs[i], pairs[i + 1] ?? DBNull.Value);
            return command;
        }
        public List<Dictionary<string, object>> Query(string sql, params object[] pairs)
        {
            var rows = new List<Dictionary<string, object>>();
            using (var command = Command(sql, pairs))
            using (var reader = command.ExecuteReader())
                while (reader.Read())
                {
                    var row = new Dictionary<string, object>();
                    for (int i = 0; i < reader.FieldCount; i++) row[reader.GetName(i)] = reader.IsDBNull(i) ? null : reader.GetValue(i);
                    rows.Add(row);
                }
            return rows;
        }
        public int Execute(string sql, params object[] pairs)
        {
            using (var command = Command(sql, pairs)) return command.ExecuteNonQuery();
        }
        public int Scalar(string sql, params object[] pairs)
        {
            using (var command = Command(sql, pairs)) return Convert.ToInt32(command.ExecuteScalar());
        }
        public void Commit() { transaction.Commit(); transaction.Dispose(); transaction = null; }
        public void Dispose() { if (transaction != null) transaction.Dispose(); connection.Dispose(); }
    }
}
