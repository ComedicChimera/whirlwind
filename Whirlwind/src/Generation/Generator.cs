using System;
using System.Collections.Generic;
using System.Text;

using LLVMSharp;

using Whirlwind.Semantic;

namespace Whirlwind.Generation
{
    class Generator
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
