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
        // globally used delegates
        private delegate bool FnBodyBuilder(LLVMValueRef vref);

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
        // store the global scope of the program
        private readonly Dictionary<string, LLVMValueRef> _globalScope;
        // store globally declared structures
        private readonly Dictionary<string, LLVMTypeRef> _globalStructs;
        // store function blocks that are awaiting generation
        private readonly List<Tuple<LLVMValueRef, BlockNode>> _fnBlocks;
        // store function blocks with special generation algorithms that are awaiting generation (delayed)
        private readonly List<Tuple<LLVMValueRef, FnBodyBuilder>> _fnSpecialBlocks;

        // store the current generic suffix (will be appended to everything that is visited)
        private string _genericSuffix = "";

        // store the randomly generated package prefix
        private readonly string _randPrefix;
        // store global string type
        private LLVMTypeRef _stringType;

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
            _globalScope = new Dictionary<string, LLVMValueRef>();
            _globalStructs = new Dictionary<string, LLVMTypeRef>();
            _fnBlocks = new List<Tuple<LLVMValueRef, BlockNode>>();
            _fnSpecialBlocks = new List<Tuple<LLVMValueRef, FnBodyBuilder>>();

            string randPrefixVals = "0123456789abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ";

            var randGenerator = new Random();
            _randPrefix = string.Concat(Enumerable.Repeat(0, 16).Select(x =>
            {
                int rand = randGenerator.Next() % randPrefixVals.Length;

                return randPrefixVals[rand];
            })) + ".";
        }

        public void Generate(BlockNode tree, string outputFile)
        {
            // first node is Package
            foreach (var node in tree.Block)
                _generateTopDecl(node);

            // build each fn block awaiting completion
            foreach (var fb in _fnBlocks)
                _buildFunctionBlock(fb.Item1, fb.Item2);

            // build each fn block with a special generation algo (like a constructor, etc.)
            foreach (var fsb in _fnSpecialBlocks)
                _buildFunctionBlock(fsb.Item1, fsb.Item2);

            // add in any special functions / post generation code here

            if (LLVM.VerifyModule(_module, LLVMVerifierFailureAction.LLVMPrintMessageAction, out var error) != new LLVMBool(0))
                Console.WriteLine("LLVM Build Errors");

            LLVM.DumpModule(_module);
        }

        private void _generateTopDecl(ITypeNode node)
        {
            switch (node.Name)
            {
                case "AnnotatedBlock":
                    {
                        var annotBlock = (BlockNode)node;
                        var annotation = (StatementNode)annotBlock.Block[0];

                        string annotName = ((ValueNode)annotation.Nodes[0]).Value;

                        _generateTopDecl(annotBlock.Block[1]);

                        if (annotName == "impl" && ((ValueNode)annotation.Nodes[1]).Value == "string")
                            _stringType = _globalStructs["__string"];

                        // add more annotation logic later...
                    }
                    break;
                // function bodies visited later
                case "Function":
                case "AsyncFunction":
                    // only generated function tops
                    _generateFunction((BlockNode)node, false, true);
                    break;
                case "Struct":
                    _generateStruct((BlockNode)node, false, false);
                    break;
                case "Interface":
                    _generateInterf((BlockNode)node);
                    break;
                case "Generic":
                    _generateGeneric((BlockNode)node);
                    break;
            }
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
            return _globalScope[name];
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
