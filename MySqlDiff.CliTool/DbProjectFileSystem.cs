using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace MySqlDiff
{
    public static class DbProjectFileSystem
    {
        public static DbProject CreateFromDirectory(string projectDirectory)
        {
            var stmts = new List<Statement>();
            stmts.AddRange(ReadSqlDirectory(Path.Combine(projectDirectory, "tables")));
            stmts.AddRange(ReadSqlDirectory(Path.Combine(projectDirectory, "procedures")));
            stmts.AddRange(ReadSqlDirectory(Path.Combine(projectDirectory, "triggers")));
            stmts.AddRange(ReadSqlDirectory(Path.Combine(projectDirectory, "seeds")));

            return new DbProject()
            {
                Statements = stmts,
            };
        }

        static List<Statement> ReadSqlDirectory(string directoryName)
        {
            var result = new List<Statement>();
            if (!Directory.Exists(directoryName))
            {
                return result;
            }

            var files = Directory.GetFiles(directoryName, "*.sql");

            foreach (var file in files)
            {
                var stmts = DbProject.ReadSql(file);
                if (stmts == null)
                {
                    return null;
                }

                result.AddRange(stmts);
            }

            return result;
        }

        public static void SaveAsNewDirectory(DbProject project, string projectDirectory)
        {
            Directory.CreateDirectory(Path.Combine(projectDirectory, "triggers"));
            Directory.CreateDirectory(Path.Combine(projectDirectory, "tables"));
            Directory.CreateDirectory(Path.Combine(projectDirectory, "procedures"));
            Directory.CreateDirectory(Path.Combine(projectDirectory, "states"));
            Directory.CreateDirectory(Path.Combine(projectDirectory, "seeds"));

            var seeds = new HashSet<string>();
            foreach (var statement in project.Statements)
            {
                if (statement is CreateTableStatement createTable)
                {
                    var sb = new StringBuilder();
                    DatabaseDiff.WriteCreateTable(createTable, sb);
                    var path = Path.Combine(projectDirectory, "tables", createTable.TableName + ".sql");
                    File.WriteAllText(path, sb.ToString());

                }
                else if (statement is CreateTriggerStatement createTrigger)
                {
                    var sb = new StringBuilder();
                    DatabaseDiff.WriteTrigger(createTrigger, sb);
                    var path = Path.Combine(projectDirectory, "triggers", createTrigger.TriggerName + ".sql");
                    File.WriteAllText(path, sb.ToString());
                }
                else if (statement is CreateProcedureStatement createProcedure)
                {
                    var sb = new StringBuilder();
                    DatabaseDiff.WriteProcedure(createProcedure, sb);
                    var path = Path.Combine(projectDirectory, "procedures", createProcedure.Name + ".sql");
                    File.WriteAllText(path, sb.ToString());
                }
                else if (statement is InsertStatement insert)
                {
                    var sb = new StringBuilder();
                    DatabaseDiff.WriteInsert(insert, sb);
                    // Unique seed name: group seeds by table name
                    var path = Path.Combine(projectDirectory, "seeds", insert.TableName+ ".sql");
                    if (seeds.Contains(insert.TableName))
                    {
                        File.AppendAllText(path, sb.ToString());
                    }
                    else
                    {
                        File.WriteAllText(path, sb.ToString());
                        seeds.Add(insert.TableName);
                    }
                }
                else
                {
                    throw new InvalidOperationException("unhandled");
                }
            }
        }
    }
}
