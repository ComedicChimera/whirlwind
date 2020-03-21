using LLVMSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using Whirlwind.Semantic;
using Whirlwind.Types;

// NOTE TO READER:
// All code in the Generation namespace that treats dictionaries as if they were ordered
// does so in the knowledge that because no new elements are being added to those dictionaries,
// the current order will be preserved (eg. the Members dictionary of a struct will stay in order)

namespace Whirlwind.Generation
{
    struct GeneratorSymbol
    {
        public LLVMValueRef Vref;
        public bool IsPointer;

        public GeneratorSymbol(LLVMValueRef vref)
        {
            Vref = vref;
            IsPointer = false;
        }

        public GeneratorSymbol(LLVMValueRef vref, bool isPtr)
        {
            Vref = vref;
            IsPointer = isPtr;
        }
    }

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
        private readonly List<Dictionary<string, GeneratorSymbol>> _scopes;
        // store the global scope of the program
        private readonly Dictionary<string, GeneratorSymbol> _globalScope;
        // store globally declared structures
        private readonly Dictionary<string, LLVMTypeRef> _globalStructs;
        // store function blocks that are awaiting generation
        private readonly List<Tuple<LLVMValueRef, BlockNode>> _fnBlocks;
        // store function blocks with special generation algorithms that are awaiting generation (delayed)
        private readonly List<Tuple<LLVMValueRef, FnBodyBuilder>> _fnSpecialBlocks;
        // store the classification values of data types of mapped to said data types for fast lookups
        private readonly Dictionary<ulong, DataType> _cValLookupTable;

        // store the current generic suffix (will be appended to everything that is visited)
        private string _genericSuffix = "";
        // store the current "this" pointer type
        private LLVMTypeRef _thisPtrType;
        // store the current function value reference (for append blocks)
        private LLVMValueRef _currFunctionRef;
        // store the current break and continue labels
        private LLVMBasicBlockRef _breakLabel, _continueLabel;

        // store the randomly generated package prefix
        private readonly string _randPrefix;

        // store global string type
        private LLVMTypeRef _stringType;
        // store global any type struct
        private LLVMTypeRef _anyType;
        // store reusable "fake" interface type (for forced boxing)
        private LLVMTypeRef _interfBoxType;
        // store a quick i8 pointer type
        private LLVMTypeRef _i8PtrType;

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
            _scopes = new List<Dictionary<string, GeneratorSymbol>>();
            _globalScope = new Dictionary<string, GeneratorSymbol>();
            _globalStructs = new Dictionary<string, LLVMTypeRef>();
            _fnBlocks = new List<Tuple<LLVMValueRef, BlockNode>>();
            _fnSpecialBlocks = new List<Tuple<LLVMValueRef, FnBodyBuilder>>();
            _cValLookupTable = new Dictionary<ulong, DataType>();

            string randPrefixVals = "0123456789abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ";

            var randGenerator = new Random();
            _randPrefix = string.Concat(Enumerable.Repeat(0, 16).Select(x =>
            {
                int rand = randGenerator.Next() % randPrefixVals.Length;

                return randPrefixVals[rand];
            })) + ".";

            // setup interf forced box type
            _interfBoxType = LLVM.StructCreateNamed(_ctx, "__interfBoxType");
            _interfBoxType.StructSetBody(
                new[]
                {
                    _i8PtrType,
                    _i8PtrType,
                    LLVM.Int16Type(),
                    LLVM.Int32Type()
                },
                false
            );

            _i8PtrType = LLVM.PointerType(LLVM.Int8Type(), 0);
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

            // TODO: add in any special functions / post generation code here

            if (LLVM.VerifyModule(_module, LLVMVerifierFailureAction.LLVMPrintMessageAction, out var error) != new LLVMBool(0))
                Console.WriteLine("\nFailed to build LLVM module.");

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

                        if (annotName == "impl")
                        {
                            var implName = ((ValueNode)annotation.Nodes[1]).Value;

                            if (implName == "string")
                                _stringType = _globalStructs["__string"];
                            else if (implName == "any")
                                _anyType = _globalStructs["__any"];
                        }                     

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
                case "TypeClass":
                    _generateTypeClass((BlockNode)node);
                    break;
                case "Generic":
                    _generateGeneric((BlockNode)node);
                    break;
                case "BindInterface":
                    _generateInterfBind((BlockNode)node);
                    break;
                case "BindGenericInterface":
                    _generateGenericBind((BlockNode)node);
                    break;
            }
        }

        private string _getLookupName(string name)
        {
            name = name.Replace("TypeInterf:", "");

            int i = 0;
            for (; i < _namePrefix.Length && i < name.Length; i++)
            {
                if (name[i] != _namePrefix[i])
                    break;
            }

            return string.Join("", name.Skip(i));
        }

        private string _getLookupName(DataType dt)
        {
            var rawName = _getLookupName(dt.LLVMName());

            Symbol symbol = _getSymbolFromLookupName(rawName);
            if (symbol != null && dt.Classify() != TypeClassifier.GENERIC && 
                symbol.DataType is GenericType gt)
            {
                return rawName + ".variant." + string.Join(",", gt.Generates.Single(x => x.Type.GenerateEquals(dt))
                    .GenericAliases.Select(x => x.Value.LLVMName()));
            }

            return rawName;
        }

        private Symbol _getSymbolFromLookupName(string lName)
        {
            var lNameComponents = lName.Split("::");

            _table.Lookup(lNameComponents[0], out Symbol symbol);

            // descend package tree
            if (lNameComponents.Length > 1)           
            {
                // we know the keys must exist if it makes it this far
                foreach (var item in lNameComponents.Skip(1))
                {
                    var pkg = (PackageType)symbol.DataType;

                    symbol = pkg.ExternalTable[item];
                }      
            }

            return symbol;
        }

        // only used to get the composite identifier for a lookup (handles static get for packages)
        private string _getIdentifierName(ITypeNode node)
        {
            if (node is IdentifierNode idNode)
                return idNode.IdName;
            else if (node.Name == "StaticGet")
            {
                var staticGetNode = (ExprNode)node;

                return _getIdentifierName(staticGetNode.Nodes[0]) + "::" + ((IdentifierNode)staticGetNode.Nodes[1]).IdName;
            }
            else
                throw new NotImplementedException("Function is only valid for package static get nodes and identifier nodes.");
        }

        private GeneratorSymbol _getNamedValue(string name)
        {
            IEnumerable<Dictionary<string, GeneratorSymbol>> localScopes = _scopes;

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

        private LLVMValueRef[] _insertFront(LLVMValueRef[] arr, LLVMValueRef val)
        {
            var newArr = new LLVMValueRef[arr.Length + 1];
            newArr[0] = val;
            arr.CopyTo(newArr, 1);

            return newArr;
        }

        private void _addGlobalDecl(string name, LLVMValueRef vref)
        {
            _globalScope[name] = new GeneratorSymbol(vref);
        }

        private LLVMValueRef _loadGlobalValue(string name)
            => _loadValue(_globalScope[name]);

        private LLVMValueRef _loadValue(GeneratorSymbol genSym)
        {
            if (genSym.IsPointer)
                return LLVM.BuildLoad(_builder, genSym.Vref, "load_tmp");

            return genSym.Vref;
        }

        private bool _isVTableMethod(InterfaceType it, string name)
        {
            foreach (var item in it.Implements)
            {
                if (item.GetFunction(name, out Symbol _))
                    return true;
            }

            return false;
        } 
        
        // TODO: special method cases on copies
        private LLVMValueRef _copyRefType(LLVMValueRef vref, DataType dt)
        {
            var copyRef = LLVM.BuildAlloca(_builder, _convertType(dt), "refcopy_tmp");

            var i8CopyRef = LLVM.BuildBitCast(_builder, copyRef, _i8PtrType, "refcopy_i8ptr_tmp");
            var i8SrcRef = LLVM.BuildBitCast(_builder, vref, _i8PtrType, "copysrc_i8ptr_tmp");

            LLVM.BuildCall(_builder, _globalScope["__memcpy"].Vref, 
                new[] { i8CopyRef, i8SrcRef, LLVM.ConstInt(LLVM.Int32Type(), dt.SizeOf(), new LLVMBool(0)) }, "");

            switch (dt.Classify())
            {
                case TypeClassifier.ANY:
                    _elemDeepCopy(copyRef, 0, 2);
                    break;
                // if a type class has made it this far, then we can assume it is a reference type
                case TypeClassifier.TYPE_CLASS_INSTANCE:
                    _elemDeepCopy(copyRef, 1, 2);
                    break;
                case TypeClassifier.INTERFACE_INSTANCE:
                    _elemDeepCopy(copyRef, 0, 3);
                    break;
            }

            return copyRef;
        }

        private void _elemDeepCopy(LLVMValueRef baseCopy, uint dataNdx, uint sizeNdx)
        {
            var dataElemPtr = LLVM.BuildStructGEP(_builder, baseCopy, dataNdx, "deepcopy_data_elem_ptr_tmp");
            var dataPtr = LLVM.BuildLoad(_builder, dataElemPtr, "deepcopy_data_ptr_tmp");

            var i8DataSrcPtr = LLVM.BuildBitCast(_builder, dataPtr, _i8PtrType, "deepcopy_data_src_i8ptr_tmp");
            var i8DataCopyPtr = LLVM.BuildAlloca(_builder, LLVM.Int8Type(), "deepcopy_data_copy_ptr_tmp");

            var sizeElemPtr = LLVM.BuildStructGEP(_builder, baseCopy, sizeNdx, "deepcopy_size_elem_ptr_tmp");
            var size = LLVM.BuildLoad(_builder, sizeElemPtr, "deepcopy_size_tmp");

            LLVM.BuildCall(_builder, _globalScope["__memcpy"].Vref, 
                new[] { i8DataCopyPtr, i8DataSrcPtr, size }, "");

            LLVM.BuildStore(_builder, i8DataCopyPtr, dataElemPtr);
        }

        private LLVMValueRef _copy(LLVMValueRef vref, DataType dt)
        {
            if (dt.Category == ValueCategory.RValue)
                return vref;

            if (_isReferenceType(dt))
                return _copyRefType(vref, dt);

            return vref;
        }

        private void _setVar(string name, LLVMValueRef val, bool isPtr=false)
        {
            _scopes.Last()[name] = new GeneratorSymbol(val, isPtr);
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
