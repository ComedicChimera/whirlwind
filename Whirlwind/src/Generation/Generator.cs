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
        private readonly Dictionary<string, DataType> _impls;
        private readonly string _namePrefix;

        // llvm build data
        private readonly LLVMModuleRef _module;
        private readonly LLVMBuilderRef _builder;
        private readonly LLVMContextRef _ctx;

        // keeps track of current scope hierarchy (starting from upper level function scope, not global scope)
        private readonly List<Dictionary<string, LLVMValueRef>> _scopes;

        // store global string type
        private readonly LLVMTypeRef _stringType;

        public Generator(SymbolTable table, Dictionary<string, string> flags, Dictionary<string, DataType> impls, string namePrefix)
        {
            _table = table;
            _flags = flags;
            _impls = impls;
            _namePrefix = namePrefix;

            // pass in necessary config data
            _module = LLVM.ModuleCreateWithName("test");
            _ctx = LLVM.GetModuleContext(_module);
            _builder = LLVM.CreateBuilderInContext(_ctx);

            // setup generator state data
            _scopes = new List<Dictionary<string, LLVMValueRef>>();
            _stringType = _convertType(impls["string"]);
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
                Console.WriteLine("LLVM Build Errors");

            LLVM.DumpModule(_module);
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

        private LLVMValueRef _getNamedValue(string name)
        {
            IEnumerable<Dictionary<string, LLVMValueRef>> localScopes = _scopes;

            foreach (var scope in localScopes.Reverse())
            {
                if (scope.ContainsKey(name))
                    return scope[name];
            }

            // if it does not exist in local scopes, then it is a global
            return LLVM.GetNamedGlobal(_module, name);
        }

        private LLVMValueRef _ignoreValueRef()
        {
            return LLVM.ConstInt(LLVM.Int32Type(), 0, new LLVMBool(0));
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
