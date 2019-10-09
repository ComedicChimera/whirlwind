using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;

using Whirlwind.Syntax;
using Whirlwind.Semantic;
using Whirlwind.Generation;

namespace Whirlwind
{
    static class ErrorDisplay
    {
        static Dictionary<int, string> _loadedFiles;

        static public void DisplayError(string text, string fileName, InvalidSyntaxException isnex)
        {
            if (isnex.Tok.Type == "EOF")
                Console.WriteLine("Unexpected end of file in " + fileName);
            else
                _writeError(text, "Unexpected token", isnex.Tok.Index, isnex.Tok.Value.Length, fileName);
        }

        static public void DisplayError(Package pkg, SemanticException smex)
        {
            string text;

            if (_loadedFiles.ContainsKey(smex.FilePosition))
                text = _loadedFiles[smex.FilePosition];
            else
            {
                text = File.ReadAllText(pkg.Files.Keys.ElementAt(smex.FilePosition));
                _loadedFiles[smex.FilePosition] = text;
            }
        }

        static public void DisplayError(GeneratorException gex, string outputFile)
        {

        } 

        static public void InitLoadedFiles()
        {
            _loadedFiles = new Dictionary<int, string>();
        }

        static public void ClearLoadedFiles()
        {
            _loadedFiles.Clear();
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
