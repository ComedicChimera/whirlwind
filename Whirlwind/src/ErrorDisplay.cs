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
        static Dictionary<string, string> _loadedFiles;

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

            if (_loadedFiles.ContainsKey(smex.FileName))
                text = _loadedFiles[smex.FileName];
            else
            {
                text = File.ReadAllText(smex.FileName);
                _loadedFiles[smex.FileName] = text;
            }

            _writeError(text, smex.Message, smex.Position.Start, smex.Position.Length, 
                Path.GetRelativePath(Directory.GetCurrentDirectory(), smex.FileName));
        }

        static public void DisplayError(PackageAssemblyException pae, string package)
        {
            if (pae.SymbolB == "")
                Console.WriteLine($"Multiple definitions for non-overloadable global symbol `{pae.SymbolA}` in package `{package}`");
            else
                Console.WriteLine($"Global symbols `{pae.SymbolA}` and `{pae.SymbolB}` are recursively defined in package `{package}`");
        }

        static public void DisplayError(GeneratorException gex, string outputFile)
        {
            Console.WriteLine($"Generation Error: {gex.ErrorMessage} in `{outputFile}`");
        } 

        static public void InitLoadedFiles()
        {
            _loadedFiles = new Dictionary<string, string>();
        }

        static public void ClearLoadedFiles()
        {
            _loadedFiles.Clear();
        }

        static private void _writeError(string text, string message, int position, int length, string fileName)
        {
            int line = _getLine(text, position), column = _getColumn(text, position);
            Console.WriteLine($"{message} at (Line: {line}, Column: {column}) in {fileName}\n");

            int showFrom = Enumerable.Range(0, position).Reverse().Where(i => text[i] == '\n').FirstOrDefault();
            int showCount = text.Skip(showFrom).Select((ch, i) => new { ch, i })
                .SkipWhile(x => x.i < length || x.ch != '\n').FirstOrDefault()?.i ?? (text.Length - showFrom);

            int startArrowPos = position - showFrom, endArrowPos = startArrowPos + length;

            string errorText = text.Substring(showFrom, showCount);
            string arrows = string.Join("", errorText.Select((c, i) =>
            {
                if ("\r\t\n".Contains(c))
                    return c;
                else if (i < startArrowPos || i >= endArrowPos)
                    return ' ';
                else
                    return '^';
            }));

            string[] errorLines = errorText.Trim('\n').Split("\n"), arrowLines = arrows.Trim('\n').Split("\n");

            int maxSpaces = (line + errorLines.Length).ToString().Length;
            string padSpace = String.Concat(Enumerable.Repeat(" ", maxSpaces + 1));

            for (int i = 0; i < errorLines.Length; i++)
            {
                Console.WriteLine(" " + (line + i).ToString().PadLeft(maxSpaces) + " |\t" + errorLines[i].Trim('\t'));
                Console.WriteLine(padSpace + " |\t" + arrowLines[i].Trim('\t'));
            }

            Console.Write("\n");
        }

        static private int _getColumn(string text, int ndx)
        {
            var splitText = text.Substring(0, ndx + 1).Split('\n');
            return splitText[splitText.Count() - 1].Trim('\t').Length;
        }

        static private int _getLine(string text, int ndx)
        {
            return text.Substring(0, ndx + 1).Count(x => x == '\n') + 1;
        }
    }
}
