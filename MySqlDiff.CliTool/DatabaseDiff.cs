using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace MySqlDiff
{
    public class DatabaseDiff
    {
        public static string Diff(List<Statement> next, List<Statement> previous)
        {
            // Generates SQL with parts:
            // - DROP TABLE, TRIGGER, PROCEDURE (actual dropped)
            // - DROP CONSTRAINTS (actual dropped, or modified, or affected by foreign table)
            // - CREATE TABLE, CREATE/DROP/MODIFY COLUMN/INDEX/PROCEDURE/TRIGGER
            // - CREATE CONSTRAINTS (modified and affected)
            // - INSERT/UPDATE (seed)

            // Drop previous elements not present in next

            var previousTables = new List<CreateTableStatement>();
            var previousData = new List<TableData>();
            var nextData = new List<TableData>();

            var sql = new StringBuilder();
            foreach (var previousElement in previous)
            {
                if (previousElement is CreateTableStatement previousTable)
                {
                    var nextTable = next.Where(p => p is CreateTableStatement n && n.TableName == previousTable.TableName).FirstOrDefault();
                    if (nextTable == null)
                    {
                        sql.AppendLine($"DROP TABLE {previousTable.TableName};");
                        // TODO: remove data
                    }
                }
                else if (previousElement is CreateProcedureStatement previousProcedure)
                {
                    var nextProcedure = next.Where(p => p is CreateProcedureStatement n && n.Name == previousProcedure.Name).FirstOrDefault();
                    if (nextProcedure == null)
                    {
                        sql.AppendLine($"DROP PROCEDURE {previousProcedure.Name};");
                    }
                }
                else if (previousElement is CreateTriggerStatement previousTrigger)
                {
                    // Don't drop trigger if table was dropped too
                    var nextTriggerTable = next.Where(p => p is CreateTableStatement n && n.TableName == previousTrigger.TableName).FirstOrDefault();
                    if (nextTriggerTable != null)
                    {
                        var nextTrigger = next.Where(p => p is CreateTriggerStatement n && n.TriggerName == previousTrigger.TriggerName).FirstOrDefault();
                        if (nextTrigger == null)
                        {
                            sql.AppendLine($"DROP TRIGGER {previousTrigger.TriggerName};");
                        }
                    }
                }
                else if (previousElement is InsertStatement previousInsert)
                {
                    var tbl = GetOrCreateTable(previousData, previousInsert.TableName);
                    InsertTableData(tbl, previousInsert);
                }
                else
                {
                    throw new InvalidOperationException("Unhandled");
                }
            }

            var preForeignKeySql = new StringBuilder();
            var postForeignKeySql = new StringBuilder();

            var tableSql = new StringBuilder();

            // Add new in next, or update modified
            foreach (var nextElement in next)
            {
                if (nextElement is CreateTableStatement nextTable)
                {
                    DiffCreateTable(nextTable, previous, preForeignKeySql, tableSql, postForeignKeySql);
                }
                else if (nextElement is CreateProcedureStatement nextProcedure)
                {
                    var previousProcedure = previous.Where(p => p is CreateProcedureStatement).Cast<CreateProcedureStatement>().Where(n => n.Name == nextProcedure.Name).FirstOrDefault();
                    if (previousProcedure == null)
                    {
                        WriteProcedure(nextProcedure, sql);
                    }
                    else
                    {
                        if (CheckProcedureChanged(previousProcedure, nextProcedure))
                        {
                            tableSql.AppendLine($"DROP PROCEDURE `{previousProcedure.Name}`;");
                            WriteProcedure(nextProcedure, sql);
                        }
                    }
                }
                else if (nextElement is CreateTriggerStatement nextTrigger)
                {
                    var previousTrigger = previous.Where(p => p is CreateTriggerStatement).Cast<CreateTriggerStatement>().Where(n => n.TriggerName == nextTrigger.TriggerName).FirstOrDefault();
                    if (previousTrigger == null)
                    {
                        WriteTrigger(nextTrigger, postForeignKeySql);
                    }
                    else
                    {
                        if (CheckTriggerChanged(previousTrigger, nextTrigger))
                        {
                            postForeignKeySql.AppendLine($"DROP TRIGGER `{previousTrigger.TriggerName}`;");
                            WriteTrigger(nextTrigger, postForeignKeySql);
                        }
                    }
                }
                else if (nextElement is InsertStatement nextInsert)
                {
                    var tbl = GetOrCreateTable(nextData, nextInsert.TableName);
                    InsertTableData(tbl, nextInsert);
                }
                else
                {
                    throw new InvalidOperationException("Unhandled");
                }
            }

            sql.Append(preForeignKeySql.ToString());
            sql.Append(tableSql.ToString());
            // Data diff
            DataDiffer.DataDiff(next, previous, nextData, previousData, sql);
            sql.Append(postForeignKeySql.ToString());

            return sql.ToString();
        }

        static TableData GetOrCreateTable(List<TableData> tables, string tableName)
        {
            var tbl = tables.Where(d => d.TableName == tableName).FirstOrDefault();
            if (tbl == null)
            {
                tbl = new TableData()
                {
                    TableName = tableName,
                    Rows = new List<List<TableCellData>>(),
                };

                tables.Add(tbl);
            }

            return tbl;
        }

        static void InsertTableData(TableData table, InsertStatement insertStmt)
        {

            for (var i = 0; i < insertStmt.ValueRows.Count; i++)
            {
                var row = insertStmt.ValueRows[i];
                var cells = new List<TableCellData>();
                for (var j = 0; j < row.Count; j++)
                {
                    var value = row[j];
                    if (j >= insertStmt.Columns.Count)
                    {
                        throw new InvalidOperationException("INSERT INTO " + table.TableName + " has too many columns");
                    }

                    var col = insertStmt.Columns[j];
                    cells.Add(new TableCellData()
                    {
                        ColumnName = col,
                        Token = value,
                    });
                }

                table.Rows.Add(cells);
            }
        }

        internal static void WriteWhere(StringBuilder sql, List<string> nextPkNames, List<TableCellData> nextPkColumns)
        {
            for (var i = 0; i < nextPkNames.Count; i++)
            {
                if (i > 0)
                {
                    sql.Append(" AND ");
                }

                var name = nextPkNames[i];
                var nextPkColumn = nextPkColumns.Where(c => c.ColumnName == name).FirstOrDefault();
                sql.Append($"`{name}` = {nextPkColumn.Token}");
            }
        }

        private static void WriteCreateTablePostFk(CreateTableStatement nextTable, StringBuilder sql, StringBuilder postForeignKeySql)
        {
            // Create new table without FKs
            sql.AppendLine($"CREATE TABLE `{nextTable.TableName}` (");
            var columnsNoFk = nextTable.Columns.Where(c => !(c is TableConstraint)).ToList();
            WriteColumns(columnsNoFk, sql);
            sql.AppendLine(");");
            sql.AppendLine();

            // Put FKs in postForeignKeySql
            var nextConstraints = nextTable.Columns.Where(c => c is TableConstraint).Cast<TableConstraint>().ToList();
            foreach (var constraint in nextConstraints)
            {
                WriteAddConstraint(nextTable.TableName, constraint, postForeignKeySql);
            }
        }

        internal static void WriteCreateTable(CreateTableStatement nextTable, StringBuilder sql)
        {
            // Create new table with FKs
            sql.AppendLine($"CREATE TABLE `{nextTable.TableName}` (");
            WriteColumns(nextTable.Columns, sql);
            sql.AppendLine(");");
            sql.AppendLine();
        }

        static void DiffCreateTable(CreateTableStatement nextTable, List<Statement> previous, StringBuilder preForeignKeySql, StringBuilder sql, StringBuilder postForeignKeySql)
        {
            var previousTable = previous.Where(p => p is CreateTableStatement).Cast<CreateTableStatement>().Where(n => n.TableName == nextTable.TableName).FirstOrDefault();
            if (previousTable == null)
            {
                WriteCreateTablePostFk(nextTable, sql, postForeignKeySql);
            }
            else
            {
                // Generate changes for columns, indices and constraints
                CompareColumns(previousTable.Columns, nextTable.Columns, previousElement =>
                {
                    // Drop
                    if (previousElement is TableColumn previousColumn)
                    {
                        WriteDropColumn(nextTable.TableName, previousColumn, sql);
                    }
                    else if (previousElement is TableConstraint previousConstraint)
                    {
                        WriteDropConstraint(nextTable.TableName, previousConstraint, preForeignKeySql);
                    }
                    else if (previousElement is TableKey previousKey)
                    {
                        WriteDropKey(nextTable.TableName, previousKey, sql);
                    }
                    else
                    {
                        throw new InvalidOperationException("Unhandled");
                    }
                }, nextElement =>
                {
                    // Add
                    if (nextElement is TableColumn nextColumn)
                    {
                        WriteAddColumn(nextTable.TableName, nextColumn, sql);
                    }
                    else if (nextElement is TableConstraint nextConstraint)
                    {
                        WriteAddConstraint(nextTable.TableName, nextConstraint, postForeignKeySql);
                    }
                    else if (nextElement is TableKey nextKey)
                    {
                        WriteAddKey(nextTable.TableName, nextKey, sql);
                    }
                    else
                    {
                        throw new InvalidOperationException("Unhandled");
                    }
                }, (previousElement, nextElement) =>
                {
                    // Modify
                    if (previousElement is TableColumn previousColumn && nextElement is TableColumn nextColumn)
                    {
                        // TODO: check if affects FK in separeate pass
                        // TODO: non-destructive alter column if possible
                        WriteDropColumn(nextTable.TableName, previousColumn, sql);
                        WriteAddColumn(nextTable.TableName, nextColumn, sql);
                    }
                    else if (previousElement is TableKey previousKey && nextElement is TableKey nextKey)
                    {
                        WriteDropKey(nextTable.TableName, previousKey, sql);
                        WriteAddKey(nextTable.TableName, nextKey, sql);
                    }
                    else if (previousElement is TableConstraint previousConstraint && nextElement is TableConstraint nextConstraint)
                    {
                        WriteDropConstraint(nextTable.TableName, previousConstraint, preForeignKeySql);
                        WriteAddConstraint(nextTable.TableName, nextConstraint, postForeignKeySql);
                    }
                    else
                    {
                        throw new InvalidOperationException("Unhandled");
                    }
                });
            }
        }

        static void WriteAddColumn(string tableName, TableColumn column, StringBuilder sql)
        {
            sql.Append($"ALTER TABLE `{tableName}` ADD COLUMN ");
            WriteColumn(column, sql);
            sql.AppendLine(";");
        }

        static void WriteDropColumn(string tableName, TableColumn column, StringBuilder sql)
        {
            sql.AppendLine($"ALTER TABLE `{tableName}` DROP COLUMN `{column.Name}`;");
        }

        static void WriteAddKey(string tableName, TableKey key, StringBuilder sql)
        {
            sql.Append("CREATE ");
            if (key.Unique)
            {
                sql.Append("UNIQUE ");
            }

            sql.Append($"INDEX `{key.Name}` ON `{tableName}` (");
            WriteColumnNames(key.Columns, sql);
            sql.AppendLine(");");
        }

        static void WriteDropKey(string tableName, TableKey key, StringBuilder sql)
        {
            if (key.Primary)
            {
                sql.AppendLine($"ALTER TABLE `{tableName}` DROP PRIMARY KEY;");
            }
            else
            {
                sql.AppendLine($"DROP INDEX `{key.Name}` ON `{tableName}`;");
            }
        }

        static void WriteDropConstraint(string tableName, TableConstraint constraint, StringBuilder sql)
        {
            sql.AppendLine($"ALTER TABLE `{tableName}` DROP FOREIGN KEY `{constraint.Name}`;");
        }

        static void WriteAddConstraint(string tableName, TableConstraint constraint, StringBuilder sql)
        {
            sql.Append($"ALTER TABLE `{tableName}` ADD CONSTRAINT `{constraint.Name}` FOREIGN KEY (");
            WriteColumnNames(constraint.Columns, sql);
            sql.Append($") REFERENCES `{constraint.ReferencesTable}` (");
            WriteColumnNames(constraint.ReferencesColumns, sql);
            sql.Append(")");

            foreach (var action in constraint.Actions)
            {
                sql.Append($" ON {action.OnStmt} {action.Action}");
            }

            sql.AppendLine(";");
        }

        public static void WriteColumnNames(List<string> names, StringBuilder sql)
        {
            for (var i = 0; i < names.Count; i++)
            {
                if (i > 0)
                {
                    sql.Append(", ");
                }

                sql.Append($"`{names[i]}`");
            }
        }


        static void WriteColumns(List<TableElement> elements, StringBuilder sql)
        {
            for (var i = 0; i < elements.Count; i++)
            {
                if (i > 0)
                {
                    sql.AppendLine(", ");
                }

                sql.Append("  ");

                var arg = elements[i];
                WriteColumn(arg, sql);
            }
        }

        static void WriteColumn(TableElement arg, StringBuilder sql)
        {
            if (arg is TableColumn column)
            {
                if (!string.IsNullOrEmpty(column.Direction))
                {
                    sql.Append(column.Direction);
                    sql.Append(" ");
                }

                sql.Append($"`{column.Name}` ");
                sql.Append(string.Join(" ", column.TypeParts));
            }
            else if (arg is TableKey key)
            {
                if (key.Primary)
                {
                    sql.Append("PRIMARY KEY (");
                    WriteColumnNames(key.Columns, sql);
                    sql.Append(")");
                }
                else
                {
                    if (key.Unique)
                    {
                        sql.Append("UNIQUE ");
                    }

                    sql.Append($"KEY `{key.Name}`");
                    sql.Append(" (");
                    // sql.Append(string.Join(", ", key.Columns));
                    WriteColumnNames(key.Columns, sql);
                    sql.Append(")");
                }
            }
            else if (arg is TableConstraint constraint)
            {
                sql.Append($"CONSTRAINT `{constraint.Name}`");
                sql.Append(" FOREIGN KEY (");
                WriteColumnNames(constraint.Columns, sql);
                sql.Append($") REFERENCES `{constraint.ReferencesTable}` (");
                WriteColumnNames(constraint.ReferencesColumns, sql);
                sql.Append(")");

                foreach (var action in constraint.Actions)
                {
                    sql.Append(" ON ");
                    sql.Append(action.OnStmt);
                    sql.Append(" ");
                    sql.Append(action.Action);
                }
                // ON UPD ., ON DELETE ..
            }
            else
            {
                // throw new InvalidOperationException("Unhandled");
            }
        }

        public static void WriteProcedure(CreateProcedureStatement stmt, StringBuilder sql)
        {
            sql.AppendLine("DELIMITER ;;");
            sql.Append($"CREATE PROCEDURE `{stmt.Name}`(");
            if (stmt.Arguments.Count > 0)
            {
                sql.AppendLine();
                WriteColumns(stmt.Arguments, sql);
            }

            sql.AppendLine(")");

            // TODO: replace environment stuff, SCRIPT_ROOT

            sql.Append(stmt.Body);
            sql.AppendLine(";;");
            sql.AppendLine("DELIMITER ;");
            sql.AppendLine();
        }

        public static void WriteTrigger(CreateTriggerStatement stmt, StringBuilder sql)
        {
            sql.AppendLine("DELIMITER ;;");
            sql.AppendLine($"CREATE TRIGGER `{stmt.TriggerName}` {stmt.TriggerWhen} {stmt.TriggerStmt} ON `{stmt.TableName}` FOR EACH ROW");
            sql.Append(stmt.Body);
            sql.AppendLine(";;");
            sql.AppendLine("DELIMITER ;");
            sql.AppendLine();
        }

        public static void WriteInsert(InsertStatement stmt, StringBuilder sql)
        {
            sql.AppendLine($"INSERT INTO `{stmt.TableName}` ");
            sql.Append("(");

            // sql.Append(string.Join(", ", stmt.Columns));
            WriteColumnNames(stmt.Columns, sql);

            sql.AppendLine(") VALUES ");
            for (var j = 0; j < stmt.ValueRows.Count; j++)
            {
                if (j > 0)
                {
                    sql.AppendLine(",");
                }

                sql.Append("(");
                var row = stmt.ValueRows[j];
                for (var i = 0; i < row.Count; i++)
                {
                    if (i > 0)
                    {
                        sql.Append(", ");
                    }

                    var cell = row[i];
                    sql.Append(cell);
                }
                sql.Append(")");
            }

            sql.AppendLine(";");
        }

        static void CompareColumns(List<TableElement> previousColumns, List<TableElement> nextColumns, Action<TableElement> onDropElement, Action<TableElement> onAddElement, Action<TableElement, TableElement> onModifyElement)
        {
            // Drop previous not present in next
            foreach (var previousElement in previousColumns)
            {
                var nextElement = nextColumns.Where(c => MatchTableElement(previousElement, c)).FirstOrDefault();
                if (nextElement == null)
                {
                    onDropElement(previousElement);
                }
            }

            // Add new in next, or update modified
            foreach (var nextElement in nextColumns)
            {
                var previousElement = previousColumns.Where(c => MatchTableElement(nextElement, c)).FirstOrDefault();
                if (previousElement == null)
                {
                    onAddElement(nextElement);
                }
                else
                {
                    if (CheckElementChanged(previousElement, nextElement))
                    {
                        onModifyElement(previousElement, nextElement);
                    }
                }
            }
        }

        static bool MatchTableElement(TableElement lhs, TableElement rhs)
        {
            if (lhs is TableColumn lhsColumn && rhs is TableColumn rhsColumn)
            {
                return lhsColumn.Name == rhsColumn.Name;
            }

            if (lhs is TableKey lhsKey && rhs is TableKey rhsKey)
            {
                // kinda handles primary key, since name is null only in those
                return (lhsKey.Name == rhsKey.Name);
            }

            if (lhs is TableConstraint lhsConstraint && rhs is TableConstraint rhsConstraint)
            {
                return lhsConstraint.Name == rhsConstraint.Name;
            }

            return false;
        }


        static bool CheckElementChanged(TableElement lhs, TableElement rhs)
        {
            if (lhs is TableColumn lhsColumn && rhs is TableColumn rhsColumn)
            {
                return CheckColumnChanged(lhsColumn, rhsColumn);
            }

            if (lhs is TableKey lhsKey && rhs is TableKey rhsKey)
            {
                return CheckKeyChanged(lhsKey, rhsKey);
            }

            if (lhs is TableConstraint lhsConstraint && rhs is TableConstraint rhsConstraint)
            {
                return CheckConstraintChanged(lhsConstraint, rhsConstraint);
            }

            return false;
        }

        static bool CheckColumnChanged(TableColumn lhs, TableColumn rhs)
        {
            if (lhs.TypeParts.Count != rhs.TypeParts.Count)
            {
                return true;
            }

            for (var i = 0; i < lhs.TypeParts.Count; i++)
            {
                if (lhs.TypeParts[i] != rhs.TypeParts[i])
                {
                    return true;
                }
            }

            return false;
        }

        static bool CheckKeyChanged(TableKey lhs, TableKey rhs)
        {
            if (lhs.Columns?.Count != rhs.Columns?.Count)
            {
                return true;
            }

            for (var i = 0; i < (lhs.Columns?.Count ?? 0); i++)
            {
                if (lhs.Columns[i] != rhs.Columns[i])
                {
                    return true;
                }
            }

            if (lhs.Unique != rhs.Unique)
            {
                return true;
            }

            return false;
        }

        static bool CheckConstraintChanged(TableConstraint lhs, TableConstraint rhs)
        {
            if (lhs.Columns?.Count != rhs.Columns?.Count)
            {
                return true;
            }

            for (var i = 0; i < (lhs.Columns?.Count ?? 0); i++)
            {
                if (lhs.Columns[i] != rhs.Columns[i])
                {
                    return true;
                }
            }

            if (lhs.ReferencesTable != rhs.ReferencesTable)
            {
                return true;
            }


            if (lhs.ReferencesColumns.Count != rhs.ReferencesColumns.Count)
            {
                return true;
            }

            for (var i = 0; i < lhs.ReferencesColumns.Count; i++)
            {
                if (lhs.ReferencesColumns[i] != rhs.ReferencesColumns[i])
                {
                    return true;
                }
            }

            if (lhs.Actions.Count != rhs.Actions.Count)
            {
                return true;
            }

            for (var i = 0; i < lhs.Actions.Count; i++)
            {
                var lhsAction = lhs.Actions[i];
                var rhsAction = rhs.Actions[i];
                if (lhsAction.OnStmt != rhsAction.OnStmt)
                {
                    return true;
                }

                if (lhsAction.Action != rhsAction.Action)
                {
                    return true;
                }
            }

            return false;
        }

        static bool CheckProcedureChanged(CreateProcedureStatement lhs, CreateProcedureStatement rhs)
        {
            if (lhs.Body != rhs.Body)
            {
                return true;
            }

            if (lhs.Arguments.Count != rhs.Arguments.Count)
            {
                return true;
            }

            var changed = false;
            CompareColumns(lhs.Arguments, rhs.Arguments, c => changed = true, c => changed = true, (o, n) => changed = true);
            return changed;
        }

        static bool CheckTriggerChanged(CreateTriggerStatement lhs, CreateTriggerStatement rhs)
        {
            if (lhs.TriggerWhen != rhs.TriggerWhen)
            {
                return true;
            }

            if (lhs.TriggerStmt != rhs.TriggerStmt)
            {
                return true;
            }

            if (lhs.TableName != rhs.TableName)
            {
                return true;
            }

            if (lhs.Body != rhs.Body)
            {
                return true;
            }

            return false;
        }

    }
}
