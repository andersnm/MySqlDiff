using Dahomey.Json.Attributes;
using QUT.GplexBuffers;
using System;
using System.Collections.Generic;
using System.Text;

namespace MySqlDiff
{

    internal partial class Parser
    {
        public List<Statement> Statements { get; } = new List<Statement>();

        internal Parser(Scanner scanner)
            : base(scanner)
        {
        }
    }

    public class LexSpan : QUT.Gppg.IMerge<LexSpan>
    {
        internal int startLine;     // start line of span
        internal int startColumn;   // start column of span
        internal int endLine;       // end line of span
        internal int endColumn;     // end column of span
        internal int startIndex;    // start position in the buffer
        internal int endIndex;      // end position in the buffer
        internal ScanBuff buffer;   // reference to the buffer

        public LexSpan() { }
        public LexSpan(int sl, int sc, int el, int ec, int sp, int ep, ScanBuff bf)
        { startLine = sl; startColumn = sc; endLine = el; endColumn = ec; startIndex = sp; endIndex = ep; buffer = bf; }

        /// <summary>
        /// This method implements the IMerge interface
        /// </summary>
        /// <param name="end">The last span to be merged</param>
        /// <returns>A span from the start of 'this' to the end of 'end'</returns>
        public LexSpan Merge(LexSpan end)
        {
            return new LexSpan(startLine, startColumn, end.endLine, end.endColumn, startIndex, end.endIndex, buffer);
        }

        public override string ToString()
        {
            return buffer.GetString(startIndex, endIndex);
        }
    }

}
