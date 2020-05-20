using System;
using System.Collections.Generic;
using System.Text;
using Dahomey.Json.Attributes;

namespace MySqlDiff
{
    public class Statement
    {
    }

    [JsonDiscriminator("CREATE_PROCEDURE")]
    public class CreateProcedureStatement : Statement
    {
        public string Name { get; set; }
        public List<TableElement> Arguments { get; set; }
        public string Body { get; set; }
    }

    [JsonDiscriminator("CREATE_TABLE")]
    public class CreateTableStatement : Statement
    {
        public string TableName { get; set; }
        public List<TableElement> Columns { get; set; }
    }

    [JsonDiscriminator("CREATE_TRIGGER")]
    public class CreateTriggerStatement : Statement
    {
        public string TriggerName { get; set; }
        public string TriggerWhen { get; set; }
        public string TriggerStmt { get; set; }
        public string TableName { get; set; }
        public string Body { get; set; }
    }

    [JsonDiscriminator("INSERT_VALUES")]
    public class InsertStatement : Statement
    {
        public string TableName { get; set; }
        public List<string> Columns { get; set; }
        public List<List<string>> ValueRows { get; set; }
    }
}
