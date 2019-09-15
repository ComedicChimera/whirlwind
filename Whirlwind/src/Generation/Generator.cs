using System;
using System.Collections.Generic;
using System.Text;

using LLVMSharp;

using Whirlwind.Semantic;
using Whirlwind.Types;

namespace Whirlwind.Generation
{
    partial class Generator
    {
        // visitor extracted data
        private readonly SymbolTable _table;
        private readonly Dictionary<string, string> _flags;

        // llvm build data
        private readonly LLVMModuleRef _module;
        private readonly LLVMBuilderRef _builder;

        public Generator(SymbolTable table, Dictionary<string, string> flags)
        {
            _table = table;
            _flags = flags;

            // pass in necessary config data
            _module = new LLVMModuleRef();
            _builder = new LLVMBuilderRef();
        }

        public void Generate(ITypeNode tree, string outputFile)
        {
            // first node is Package
            foreach (var node in ((BlockNode)tree).Block)
            {
                switch (node.Name)
                {
                    case "Function":
                    case "AsyncFunction":
                        _generateFunction((BlockNode)node);
                        break;
                }
            }
        }

        private string ConvertTypeToName(DataType dt)
        {
            return dt.ToString()
                .Replace("[", "_")
                .Replace("]", "_")
                .Replace("(", "_")
                .Replace(")", "_")
                .Replace("<", ".")
                .Replace(">", "")
                .Replace(",", "$")
                .Replace("::", ".")
                .Replace("*", "$.")
                .Replace(" ", "");
        }
    }

    class GeneratorException : Exception
    {
        public string ErrorMessage;

        public GeneratorException(string message)
        {
            ErrorMessage = message;
        }
    }
}
