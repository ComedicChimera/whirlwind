using System;
using System.Collections.Generic;
using System.Linq;

using Whirlwind.Syntax;
using Whirlwind.Semantic;

namespace Whirlwind
{
    static class ErrorDisplay
    {
        static public void DisplayError(string text, string fileName, InvalidSyntaxException isnex)
        {
            if (isnex.Tok.Type == "EOF")
                Console.WriteLine("Unexpected end of file in " + fileName);
            else
                _writeError(text, "Unexpected token", isnex.Tok.Index, isnex.Tok.Value.Length, fileName);
        }

        static public void DisplayError(Package pkg, SemanticException smex)
        {

        }

        static private void _writeError(string text, string message, int position, int length, string fileName)
        {
            int line = _getLine(text, position), column = _getColumn(text, position);
            Console.WriteLine($"{message} at (Line: {line}, Column: {column}) in {fileName}");
            Console.WriteLine($"\n\t{text.Split('\n')[line].Trim('\t')}");
            Console.WriteLine("\t" + String.Concat(Enumerable.Repeat(" ", column - 1)) + String.Concat(Enumerable.Repeat("^", length)));
        }

        static private int _getColumn(string text, int ndx)
        {
            var splitText = text.Substring(0, ndx + 1).Split('\n');
            return splitText[splitText.Count() - 1].Trim('\t').Length;
        }

        static private int _getLine(string text, int ndx)
        {
            return text.Substring(0, ndx + 1).Count(x => x == '\n');
        }
    }
}
