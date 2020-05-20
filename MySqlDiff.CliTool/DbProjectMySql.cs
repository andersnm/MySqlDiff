using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using MySql.Data.MySqlClient;
using MySqlDiff.CliTool.MySql;
using Renci.SshNet;

namespace MySqlDiff
{
    public class DbProjectMySql
    {
        public static DbProject CreateFromDatabase(MySqlArguments dbInfo)
        {
            using (var ctx = new MySqlContext(dbInfo))
            {
                var conn = ctx.Connection;
                conn.Open();

                var tables = GetTableSql(conn);
                var procedures = GetProcedureSql(conn);
                var triggers = GetTriggerSql(conn);

                var result = new List<Statement>();

                foreach (var table in tables)
                {
                    var path = Path.Combine("tables", table.Key + ".sql");
                    result.AddRange(DbProject.ReadSqlFromString(path, table.Value));
                }

                foreach (var table in procedures)
                {
                    var path = Path.Combine("procedures", table.Key + ".sql");
                    result.AddRange(DbProject.ReadSqlFromString(path, table.Value));
                }

                foreach (var table in triggers)
                {
                    var path = Path.Combine("triggers", table.Key + ".sql");
                    result.AddRange(DbProject.ReadSqlFromString(path, table.Value));
                }

                return new DbProject()
                {
                    Statements = result,
                };
            }
        }

        static Dictionary<string, string> GetTableSql(MySqlConnection conn)
        {
            var names = GetListOfString(conn, "SELECT TABLE_NAME FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA=Database() AND TABLE_TYPE='BASE TABLE'");
            var result = new Dictionary<string, string>();
            foreach (var name in names)
            {
                var sql = GetString(conn, $"SHOW CREATE TABLE `{name}`", 1);
                var sb = new StringBuilder();
                sb.Append(sql);
                sb.AppendLine(";");
                result.Add(name, sb.ToString());
            }

            return result;
        }

        static Dictionary<string, string> GetProcedureSql(MySqlConnection conn)
        {
            var names = GetListOfString(conn, "SELECT ROUTINE_NAME FROM INFORMATION_SCHEMA.ROUTINES WHERE ROUTINE_SCHEMA=Database() AND ROUTINE_TYPE='PROCEDURE'");
            var result = new Dictionary<string, string>();
            foreach (var name in names)
            {
                var sql = GetString(conn, $"SHOW CREATE PROCEDURE `{name}`", 2);
                var sb = new StringBuilder();
                sb.AppendLine("DELIMITER ;;");
                sb.Append(sql);
                sb.AppendLine(";;");
                sb.AppendLine("DELIMITER ;");
                result.Add(name, sb.ToString());
            }

            return result;
        }

        static Dictionary<string, string> GetTriggerSql(MySqlConnection conn)
        {
            var names = GetListOfString(conn, "SELECT TRIGGER_NAME FROM INFORMATION_SCHEMA.TRIGGERS WHERE TRIGGER_SCHEMA=Database()");
            var result = new Dictionary<string, string>();
            foreach (var name in names)
            {
                var sql = GetString(conn, $"SHOW CREATE TRIGGER `{name}`", 2);
                var sb = new StringBuilder();
                sb.AppendLine("DELIMITER ;;");
                sb.Append(sql);
                sb.AppendLine(";;");
                sb.AppendLine("DELIMITER ;");
                result.Add(name, sb.ToString());
            }

            return result;
        }

        static List<string> GetListOfString(MySqlConnection conn, string sql)
        {
            var tableNames = new List<string>();
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = sql;

                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        tableNames.Add(reader.GetString(0));
                    }
                }
            }

            return tableNames;
        }

        static string GetString(MySqlConnection conn, string sql, int column)
        {
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = sql;

                using (var reader = cmd.ExecuteReader())
                {
                    reader.Read();
                    return reader.GetString(column);
                }
            }
        }
    }
}
