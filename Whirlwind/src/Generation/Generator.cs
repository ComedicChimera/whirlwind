﻿using System;
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

        // llvm build data
        private readonly LLVMModuleRef _module;
        private readonly LLVMBuilderRef _builder;

        public Generator(SymbolTable table, Dictionary<string, string> flags, Dictionary<string, DataType> typeImpls)
        {
            _table = table;
            _flags = flags;
            _typeImpls = typeImpls;

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
