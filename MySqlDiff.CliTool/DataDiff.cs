using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MySqlDiff
{
    class TableCellData
    {
        public string ColumnName { get; set; }
        public string Token { get; set; }
    }

    class TableData
    {
        public string TableName { get; set; }
        public List<List<TableCellData>> Rows { get; set; }
    }


    internal class DataDiffer
    {
        public static void DataDiff(List<Statement> next, List<Statement> previous, List<TableData> nextData, List<TableData> previousData, StringBuilder sql)
        {
            // Find deleted rows
            foreach (var previousTable in previousData)
            {
                var nextCreateTable = (CreateTableStatement)next.Where(p => p is CreateTableStatement cp && cp.TableName == previousTable.TableName).FirstOrDefault();
                if (nextCreateTable == null)
                {
                    // Table exists in prev, not in next: Skip delete, table will be dropped
                    continue;
                }

                List<List<TableCellData>> nextRows =
                    nextData
                        .Where(t => t.TableName == previousTable.TableName)
                        .Select(t => t.Rows)
                        .FirstOrDefault()
                    ?? new List<List<TableCellData>>();

                var previousPkNames = GetPrimaryKeys(previous, previousTable.TableName, out var previousColumns);
                var nextPkNames = GetPrimaryKeys(next, previousTable.TableName, out var nextColumns);

                // dont do this in initial create:
                //if (previousPkNames.Count != nextPkNames.Count)
                //{
                //    // in this case delete all and reinsert! we cannot reliable map these 
                //    throw new InvalidOperationException("TODO: number of primary keys changed");
                //}

                // dont do this in initial create:
                var intersect = previousPkNames.Intersect(nextPkNames).ToList();
                //if (intersect.Count != previousPkNames.Count)
                //{
                //    throw new InvalidOperationException("TODO: primary keys changed");
                //}

                foreach (var previousRow in previousTable.Rows)
                {
                    // find row in next med samme pk
                    var previousPkColumns = previousRow.Where(c => previousPkNames.Contains(c.ColumnName)).ToList();
                    if (previousPkColumns.Count == 0)
                    {
                        throw new InvalidOperationException("No primary key(s) in " + previousTable.TableName);
                    }

                    var nextPk = GetRowByPk(nextRows, previousPkColumns);

                    if (nextPk == null)
                    {
                        // row exists in prev, but not in next = delete
                        sql.Append($"DELETE FROM `{previousTable.TableName}` WHERE ");
                        DatabaseDiff.WriteWhere(sql, previousPkNames, previousPkColumns);
                        sql.AppendLine(";");
                        sql.AppendLine();
                    }
                    else
                    {
                        // row exists in both; detect changes later
                    }
                }
            }


            // Find new and updated rows
            foreach (var nextTable in nextData)
            {
                var insertSql = new StringBuilder();
                var updateSql = new StringBuilder();
                var insertTables = new HashSet<string>();
                List<List<TableCellData>> previousRows =
                    previousData
                        .Where(t => t.TableName == nextTable.TableName)
                        .Select(t => t.Rows)
                        .FirstOrDefault()
                    ?? new List<List<TableCellData>>();

                var previousPkNames = GetPrimaryKeys(previous, nextTable.TableName, out var previousColumns);
                var nextPkNames = GetPrimaryKeys(next, nextTable.TableName, out var nextColumns);

                // dont do this in initial create:
                //if (previousPkNames.Count != nextPkNames.Count)
                //{
                //    throw new InvalidOperationException("TODO: number of primary keys changed");
                //}

                var intersectCount = previousPkNames.Intersect(nextPkNames).Count();
                if (intersectCount != previousPkNames.Count)
                {
                    throw new InvalidOperationException("TODO: primary keys changed");
                }

                foreach (var nextRow in nextTable.Rows)
                {
                    // find row in next med samme pk
                    var nextPkColumns = nextRow.Where(c => previousPkNames.Contains(c.ColumnName)).ToList();
                    if (nextPkColumns.Count != previousPkNames.Count)
                    {
                        throw new InvalidOperationException("Insert row did not specify a value for all primary keys in " + nextTable.TableName);
                    }

                    // dont do this in initial create:
                    //if (nextPkColumns.Count == 0)
                    //{
                    //    throw new InvalidOperationException("No primary key(s) in " + nextTable.TableName);
                    //}

                    var previousPkRow = GetRowByPk(previousRows, nextPkColumns);

                    if (previousPkRow == null)
                    {
                        // row exists in next, but not in prev = insert
                        if (!insertTables.Contains(nextTable.TableName))
                        {
                            insertTables.Add(nextTable.TableName);
                            insertSql.AppendLine("INSERT INTO `" + nextTable.TableName + "`(");

                            DatabaseDiff.WriteColumnNames(nextColumns.Select(c => c.Name).ToList(), insertSql);
                            // insertSql.Append(string.Join(", ", nextColumns.Select(c => c.Name)));

                            insertSql.AppendLine(") VALUES");
                        }
                        else
                        {
                            insertSql.AppendLine(",");
                        }

                        //sql.AppendLine("INSERT INTO `" + nextTable.TableName + "`(");
                        //sql.Append(string.Join(", ", nextColumns.Select(c => c.Name)));
                        //sql.AppendLine(") VALUES (");

                        insertSql.Append("(");
                        for (var i = 0; i < nextColumns.Count; i++)
                        {
                            if (i > 0)
                            {
                                insertSql.Append(", ");
                            }

                            var cell = nextRow.Where(r => r.ColumnName == nextColumns[i].Name).FirstOrDefault();
                            if (cell != null)
                            {
                                insertSql.Append(cell.Token);
                            }
                            else
                            {
                                insertSql.Append("DEFAULT");
                            }
                        }
                        insertSql.Append(")");

                        // sql.AppendLine(");");
                        // sql.AppendLine();
                    }
                    else
                    {
                        // row exists in both, check if update
                        // if any of the columns are different -> update, check deleted, then new/edited

                        var updates = new List<TableCellData>();
                        foreach (var nextColumn in nextRow)
                        {
                            var p = previousPkRow.Where(c => c.ColumnName == nextColumn.ColumnName).FirstOrDefault();
                            if (p == null || p.Token != nextColumn.Token)
                            {
                                updates.Add(nextColumn);
                            }
                        }

                        if (updates.Count > 0)
                        {
                            updateSql.Append("UPDATE " + nextTable.TableName + " SET ");
                            for (var i = 0; i < updates.Count; i++)
                            {
                                if (i > 0)
                                {
                                    updateSql.Append(", ");
                                }

                                var update = updates[i];
                                if (nextPkNames.Contains(update.ColumnName))
                                {
                                    continue;
                                }

                                updateSql.Append(update.ColumnName);
                                updateSql.Append(" = ");
                                updateSql.Append(update.Token);
                            }

                            updateSql.Append(" WHERE ");
                            DatabaseDiff.WriteWhere(updateSql, nextPkNames, nextPkColumns);

                            updateSql.AppendLine(";");
                            updateSql.AppendLine();
                        }
                    }
                }

                if (insertSql.Length > 0)
                {
                    insertSql.AppendLine(";");
                    insertSql.AppendLine();
                }

                sql.Append(insertSql);
                sql.Append(updateSql);
            }

        }

        static List<string> GetPrimaryKeys(List<Statement> statements, string tableName, out List<TableColumn> schema)
        {
            var createTable = (CreateTableStatement)statements.Where(p => p is CreateTableStatement cp && cp.TableName == tableName).FirstOrDefault();
            if (createTable == null)
            {
                schema = new List<TableColumn>();
                return new List<string>();
                // throw new InvalidOperationException("Cannot insert into table without CREATE TABLE: " + tableName);
            }

            schema = createTable.Columns.Where(c => c is TableColumn).Cast<TableColumn>().ToList();
            var previousPk = createTable.Columns.Where(c => c is TableKey kc && kc.Primary).Cast<TableKey>().FirstOrDefault();
            return previousPk.Columns;
        }

        static List<TableCellData> GetRowByPk(List<List<TableCellData>> rows, List<TableCellData> pkColumns)
        {
            foreach (var row in rows)
            {
                var hasPk = true;
                foreach (var pkColumn in pkColumns)
                {
                    var nextPkColumn = row.Where(c => c.ColumnName == pkColumn.ColumnName).FirstOrDefault();
                    if (nextPkColumn == null)
                    {
                        hasPk = false;
                        break;
                    }

                    if (nextPkColumn.Token != pkColumn.Token)
                    {
                        hasPk = false;
                        break;
                    }
                }

                if (hasPk)
                {
                    return row;
                }
            }

            return null;
        }
    }
}
