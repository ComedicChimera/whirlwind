using System;
using System.Linq;
using System.Collections.Generic;

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
        private readonly Dictionary<string, DataType> _typeImpls;
        private readonly string _namePrefix;

        // llvm build data
        private readonly LLVMModuleRef _module;
        private readonly LLVMBuilderRef _builder;

        public Generator(SymbolTable table, Dictionary<string, string> flags, Dictionary<string, DataType> typeImpls, string namePrefix)
        {
            _table = table;
            _flags = flags;
            _typeImpls = typeImpls;
            _namePrefix = namePrefix;

            // pass in necessary config data
            _module = LLVM.ModuleCreateWithName("test");
            _builder = LLVM.CreateBuilder();
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
                        _generateFunction((BlockNode)node, false);
                        break;
                    case "Struct":
                        _generateStruct((BlockNode)node, false, false);
                        break;
                }
            }

            if (LLVM.VerifyModule(_module, LLVMVerifierFailureAction.LLVMPrintMessageAction, out var error) != new LLVMBool(0))
                Console.WriteLine("LLVM Build Error: " + error);

            LLVM.DumpModule(_module);
        }

        private string _convertTypeToName(DataType dt)
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

        private string _getLookupName(string name)
        {
            int i = 0;
            for (; i < _namePrefix.Length && i < name.Length; i++)
            {
                if (name[i] != _namePrefix[i])
                    break;
            }

            return string.Join("", name.Skip(i));
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
