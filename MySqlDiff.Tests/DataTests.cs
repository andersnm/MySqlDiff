using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

/*
 * Test:
 * dd index
 * dd column
 * dd foreign key
 * drop index
 * drop column
 * drop foreign key
*/

namespace MySqlDiff.Tests
{
    public class DataTests
    {
        [SetUp]
        public void Setup()
        {
        }

        [Test]
        public void InsertRow()
        {
            var previous = new List<Statement>()
            {
                GetTable1(),
                GetTable2(),
                // GetInsert1(),
            };

            var nextTable1 = GetTable1();
            var nextTable2 = GetTable2();

            var next = new List<Statement>()
            {
                nextTable1,
                nextTable2,
                GetInsert1(),
            };

            var upSql = DatabaseDiff.Diff(next, previous);
            Assert.AreEqual("INSERT INTO `test`(\r\n`test_id`, `shelf_id`, `name`, `inventory`) VALUES\r\n(1, DEFAULT, \"Name1\", DEFAULT),\r\n(2, DEFAULT, \"Name2\", DEFAULT);\r\n\r\n", upSql);

            var downSql = DatabaseDiff.Diff(previous, next);
            Assert.AreEqual("DELETE FROM `test` WHERE `test_id` = 1;\r\n\r\nDELETE FROM `test` WHERE `test_id` = 2;\r\n\r\n", downSql);
        }

        public string ReadEmbeddedRessourceToString(string resourceName)
        {
            resourceName = "MySqlDiff.Tests.expected." + resourceName;
            using (var stream = typeof(DataTests).Assembly.GetManifestResourceStream(resourceName))
            {
                if (stream != null)
                {
                    using (var reader = new StreamReader(stream))
                    {
                        return reader.ReadToEnd();
                    }
                }
            }
            return string.Empty;
        }

        [Test]
        public void InsertRowAndChangePk()
        {
            var previousTable1 = GetTable1();
            var previousTable2 = GetTable2();

            var previous = new List<Statement>()
            {
                GetTable1(),
                GetTable2(),
                // GetInsert1(),
            };

            var nextTable1 = GetTable1();
            var nextTable2 = GetTable2();

            var next = new List<Statement>()
            {
                nextTable1,
                nextTable2,
                GetInsert1(),
            };

            var pk = (TableKey)nextTable1.Columns.Where(c => c is TableKey ck && ck.Primary).First();
            pk.Columns.Add("shelf_id");

            var upSql = DatabaseDiff.Diff(next, previous);
            Assert.AreEqual(ReadEmbeddedRessourceToString("InsertRowAndChangePk.up.sql"), upSql);

            var downSql = DatabaseDiff.Diff(previous, next);
            Assert.AreEqual("DELETE FROM `test` WHERE `test_id` = 1;\r\n\r\nDELETE FROM `test` WHERE `test_id` = 2;\r\n\r\n", downSql);
        }

        CreateTableStatement GetTable1()
        {
            return new CreateTableStatement()
            {
                TableName = "test",
                Columns = new List<TableElement>() {
                    new TableColumn() {
                        Name = "test_id",
                        TypeParts = new List<string>() { "int" },
                    },
                    new TableColumn() {
                        Name = "shelf_id",
                        TypeParts = new List<string>() { "int" },
                    },
                    new TableColumn() {
                        Name = "name",
                        TypeParts = new List<string>() { "varchar(127)" },
                    },
                    new TableColumn() {
                        Name = "inventory",
                        TypeParts = new List<string>() { "int" },
                    },
                    new TableKey() {
                        Primary = true,
                        Columns = new List<string>() { "test_id" }
                    },
                    new TableKey() {
                        Unique = true,
                        Name = "test_key_1",
                        Columns = new List<string>() { "inventory" },
                    },
                    new TableConstraint() {
                        Name = "test_constraint_1",
                        Columns = new List<string>() { "other_id" },
                        ReferencesTable = "other",
                        ReferencesColumns = new List<string>() { "other_id" },
                        Actions = new List<ConstraintAction>() {
                            new ConstraintAction() {
                                OnStmt = "UPDATE",
                                Action = "RESTRICT",
                            },
                            new ConstraintAction() {
                                OnStmt = "DELETE",
                                Action = "RESTRICT",
                            },
                        }
                    },
                }
            };
        }

        CreateTableStatement GetTable2()
        {
            return new CreateTableStatement()
            {
                TableName = "other",
                Columns = new List<TableElement>() {
                    new TableColumn() {
                        Name = "other_id",
                        TypeParts = new List<string>() { "int" },
                    },
                    new TableColumn() {
                        Name = "description",
                        TypeParts = new List<string>() { "varchar(127)" },
                    },
                    new TableKey() {
                        Unique = true,
                        Name = "other_key_1",
                        Columns = new List<string>() { "description" },
                    },
                }
            };
        }

        InsertStatement GetInsert1()
        {
            return new InsertStatement()
            {
                TableName = "test",
                Columns = new List<string>()
                {
                    "test_id", "name"
                },
                ValueRows = new List<List<string>>()
                {
                    new List<string>() {
                        "1", "\"Name1\"",
                    },
                    new List<string>() {
                        "2", "\"Name2\"",
                    }
                }
            };
        }
    }
}