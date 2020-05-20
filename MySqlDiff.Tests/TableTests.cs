using NUnit.Framework;
using System;
using System.Collections.Generic;

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
    public class TableTests
    {
        [SetUp]
        public void Setup()
        {
        }

        [Test]
        public void AddColumn()
        {
            var previousTable1 = GetTable1();
            var previousTable2 = GetTable2();

            var previous = new List<Statement>()
            {
                previousTable1,
                previousTable2,
            };

            var nextTable1 = GetTable1();
            var nextTable2 = GetTable2();

            var next = new List<Statement>()
            {
                nextTable1,
                nextTable2,
            };

            previousTable1.Columns.RemoveAll(c => c is TableColumn cc && cc.Name == "name");

            var upSql = DatabaseDiff.Diff(next, previous);
            Assert.AreEqual("ALTER TABLE `test` ADD COLUMN `name` varchar(127);\r\n", upSql);

            var downSql = DatabaseDiff.Diff(previous, next);
            Assert.AreEqual("ALTER TABLE `test` DROP COLUMN `name`;\r\n", downSql);
        }

        [Test]
        public void AddConstraint()
        {
            var previousTable1 = GetTable1();
            var previousTable2 = GetTable2();

            var previous = new List<Statement>()
            {
                previousTable1,
                previousTable2,
            };

            var nextTable1 = GetTable1();
            var nextTable2 = GetTable2();

            var next = new List<Statement>()
            {
                nextTable1,
                nextTable2,
            };

            previousTable1.Columns.RemoveAll(c => c is TableConstraint cc && cc.Name == "test_constraint_1");

            var upSql = DatabaseDiff.Diff(next, previous);
            Assert.AreEqual("ALTER TABLE `test` ADD CONSTRAINT `test_constraint_1` FOREIGN KEY (`other_id`) REFERENCES `other` (`other_id`) ON UPDATE RESTRICT ON DELETE RESTRICT;\r\n", upSql);

            var downSql = DatabaseDiff.Diff(previous, next);
            Assert.AreEqual("ALTER TABLE `test` DROP FOREIGN KEY `test_constraint_1`;\r\n", downSql);
        }

        [Test]
        public void AddIndex()
        {
            var previousTable1 = GetTable1();
            var previousTable2 = GetTable2();

            var previous = new List<Statement>()
            {
                previousTable1,
                previousTable2,
            };

            var nextTable1 = GetTable1();
            var nextTable2 = GetTable2();

            var next = new List<Statement>()
            {
                nextTable1,
                nextTable2,
            };

            previousTable1.Columns.RemoveAll(c => c is TableKey cc && cc.Name == "test_key_1");

            var upSql = DatabaseDiff.Diff(next, previous);
            Assert.AreEqual("CREATE UNIQUE INDEX `test_key_1` ON `test` (`inventory`);\r\n", upSql);

            var downSql = DatabaseDiff.Diff(previous, next);
            Console.WriteLine(downSql);
            Assert.AreEqual("DROP INDEX `test_key_1` ON `test`;\r\n", downSql);
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
                        Name = "name",
                        TypeParts = new List<string>() { "varchar(127)" },
                    },
                    new TableColumn() {
                        Name = "inventory",
                        TypeParts = new List<string>() { "int" },
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

        List<Statement> GetStatements()
        {
            return new List<Statement>()
            {
                GetTable1(),
                GetTable2(),
            };
        }
    }
}