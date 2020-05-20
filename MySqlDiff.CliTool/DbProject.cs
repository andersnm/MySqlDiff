using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Dahomey.Json.Attributes;

namespace MySqlDiff
{
    public class TableElement
    {
    }

    [JsonDiscriminator("COLUMN")]
    public class TableColumn : TableElement
    {
        /// <summary>
        /// IN/OUT direction for SP arguments, which overloads this class
        /// </summary>
        public string Direction { get; set; }
        public string Name { get; set; }
        public List<string> TypeParts { get; set; }
    }

    [JsonDiscriminator("KEY")]
    public class TableKey : TableElement
    {
        public string Name { get; set; }
        public List<string> Columns { get; set; }
        public bool Primary { get; set; }
        public bool Unique { get; set; }
    }

    public class ConstraintAction
    {
        public string OnStmt { get; set; }
        public string Action { get; set; }
    }

    [JsonDiscriminator("CONSTRAINT")]
    public class TableConstraint : TableElement
    {
        public string Name { get; set; }
        public List<string> Columns { get; set; }
        public string ReferencesTable { get; set; }
        public List<string> ReferencesColumns { get; set; }
        public List<ConstraintAction> Actions { get; set; }
    }

    public class DbProject
    {
        public List<Statement> Statements { get; set; }

        public static List<Statement> ReadSql(string fileName)
        {
            using (var strm = new FileStream(fileName, FileMode.Open, FileAccess.Read))
            {
                return ParseSql(strm, fileName);
            }
        }

        public static List<Statement> ReadSqlFromString(string fileName, string sql)
        {
            var bytes = Encoding.UTF8.GetBytes(sql);
            using (var strm = new MemoryStream(bytes))
            {
                return ParseSql(strm, fileName);
            }
        }

        public static List<Statement> ParseSql(Stream strm, string fileName)
        {
            var scanner = new Scanner(strm, "UTF-8");
            var parser = new Parser(scanner);

            var success = parser.Parse();
            if (!success)
            {
                var errorToken = scanner.TokenSpan();
                Console.WriteLine(fileName + "(" + errorToken.startLine + "," + errorToken.startColumn + "): error: syntax error at token '" + scanner.yytext + "'");

                return null;
            }

            return parser.Statements;
        }
    }
}
