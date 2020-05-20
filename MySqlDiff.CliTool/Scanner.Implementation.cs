using System;

namespace MySqlDiff
{
    internal sealed partial class Scanner : ScanBase
    {
        public string CurrentDelimiter { get; set; } = ";";

        public LexSpan TokenSpan()
        {
            return new LexSpan(tokLin, tokCol, tokELin, tokECol, tokPos, tokEPos, buffer);
        }

        public override void yyerror(string format, params object[] args)
        {
            Console.WriteLine(format, args);
        }
    }
}
