using System.Collections.Generic;
using System.Linq;

using LLVMSharp;

using Whirlwind.Semantic;
using Whirlwind.Types;

namespace Whirlwind.Generation
{
    partial class Generator
    {
        private delegate bool InternalBuilderAlgo(LLVMValueRef vref, BlockNode block);

        private void _generateStruct(BlockNode node, bool exported, bool packed)
        {
            var name = ((IdentifierNode)node.Nodes[0]).IdName + _genericSuffix;
            var st = (StructType)node.Nodes[0].Type;

            var llvmStruct = LLVM.StructCreateNamed(_ctx, name);
            llvmStruct.StructSetBody(st.Members.Select(x => _convertType(x.Value.DataType, true)).ToArray(), packed);

            _globalStructs[name] = llvmStruct;

            var memberDict = new Dictionary<string, ITypeNode>();

            // process constructor
            bool hasMultipleConstructors = st.HasMultipleConstructors();
            foreach (var item in node.Block)
            {
                if (item is ExprNode eNode)
                {
                    foreach (var elem in eNode.Nodes.Skip(1))
                        memberDict.Add(((IdentifierNode)elem).IdName, eNode.Nodes[0]);
                }
                else
                {
                    BlockNode constructor = (BlockNode)item;
                    FunctionType cft = (FunctionType)(constructor.Nodes[0].Type).Copy();
                    cft.Parameters.Insert(0, _buildThisParameter(st));

                    string suffix = ".constructor";

                    if (hasMultipleConstructors)
                        suffix += "." + string.Join(",", cft.Parameters.Select(x => x.DataType.LLVMName()));

                    var llvmConstructor = _generateFunctionPrototype(name + suffix, cft, exported);
                    _addGlobalDecl(name + suffix, llvmConstructor);

                    _appendFunctionBlock(llvmConstructor, constructor);
                }
            }

            // add custom init members build algo and append
            var initFnProto = _generateFunctionPrototype(name + "._$initMembers", new FunctionType(new List<Parameter>
                            { _buildThisParameter(st) },
                       new NoneType(), false), false);

            _addGlobalDecl(name + "._$initMembers", initFnProto);

            _appendFunctionBlock(initFnProto, (initFn) =>
            {
                // build init members content
                for (int i = 0; i < st.Members.Count; i++)
                {
                    var memberPtr = LLVM.BuildStructGEP(_builder, _getNamedValue("this").Vref, (uint)i, "get_member_tmp");

                    string memberName = st.Members.ElementAt(i).Key;
                    var memberValue = memberDict.ContainsKey(memberName) ?
                        _generateExpr(memberDict[memberName]) :
                        _getNullValue(st.Members.ElementAt(i).Value.DataType);

                    LLVM.BuildStore(_builder, memberValue, memberPtr);
                }

                return true;
            });
        }

        private Parameter _buildThisParameter(DataType dt)
            => new Parameter("this", new PointerType(dt, false), false, false, false, false);

        // TODO: external interfaces
        private void _generateInterf(BlockNode node)
        {
            var idNode = (IdentifierNode)node.Nodes[0];
            var interfType = (InterfaceType)idNode.Type;

            string name = idNode.IdName;

            _table.Lookup(idNode.IdName, out Symbol interfSymbol);
            bool exported = interfSymbol.Modifiers.Contains(Modifier.EXPORTED);

            string llvmPrefix = exported ? _randPrefix : "";
            name += _genericSuffix;

            var methods = new List<LLVMTypeRef>();

            int methodNdx = 0;
            foreach (var method in interfType.Methods)
            {
                if (method.Key.DataType is FunctionType fnType)
                {
                    string llvmName;
                    if (_getOperatorOverloadName(method.Key.Name, out string overloadName))
                        llvmName = name + ".operator." + overloadName;
                    else
                        llvmName = name + ".interf." + method.Key.Name;

                    if (interfType.Methods.Where(x => x.Key.Name == method.Key.Name).Count() > 1)
                        llvmName += "." + string.Join(",", fnType.Parameters.Select(x => x.DataType.LLVMName()));

                    methods.Add(_convertType(fnType));

                    if (method.Value != MethodStatus.ABSTRACT)
                    {
                        var llvmMethod = _generateFunctionPrototype(llvmName, fnType, exported);

                        _appendFunctionBlock(llvmMethod, ((BlockNode)node.Block[methodNdx]));
                    }
                }
                // generic case
                else
                {
                    var generateList = _generateGenericMethod(name, method.Key);

                    if (method.Value != MethodStatus.ABSTRACT)
                    {
                        foreach (var generate in generateList)
                        {
                            var llvmMethod = _generateFunctionPrototype(generate.Item1, generate.Item2, exported);

                            _appendFunctionBlock(llvmMethod, generate.Item3);
                        }
                    }                    
                }
            }

            var vtableStruct = LLVM.StructCreateNamed(_ctx, llvmPrefix + name + ".__vtable");
            vtableStruct.StructSetBody(methods.ToArray(), false);

            _globalStructs[name + ".__vtable"] = vtableStruct;

            var interfStruct = LLVM.StructCreateNamed(_ctx, llvmPrefix + name);
            interfStruct.StructSetBody(new[]
            {
                LLVM.PointerType(LLVM.Int8Type(), 0),
                LLVM.PointerType(vtableStruct, 0),
                LLVM.Int16Type()
            }, false);

            _globalStructs[name] = interfStruct;
        }

        Dictionary<string, string> _opNameTable = new Dictionary<string, string>
        {
            { "+", "Add" },
            { "-", "Sub" },
            { "*", "Mul" },
            { "/", "Div" },
            { "~/", "Floordiv" },
            { "~^", "Pow" },
            { "%", "Mod" },
            { ">", "Gt" },
            { "<", "Lt" },
            { ">=", "GtEq" },
            { "<=", "LtEq" },
            { "==", "Eq" },
            { "!=", "Neq" },
            { "!", "Not" },
            { "AND", "And" },
            { "OR", "Or" },
            { "XOR", "Xor" },
            { "~", "Complement" },
            { ">>", "RShift" },
            { "<<", "LShift" },
            { "~*", "Compose" },
            { ">>=", "Bind" },
            { "[]", "Subscript" },
            { "[:]", "Slice" },
            { "..", "Range" },
            { ":>", "ExtractInto" }
        };

        private bool _getOperatorOverloadName(string baseName, out string outputName)
        {
            string trimmedName = baseName.Trim('_');
            
            if (_opNameTable.ContainsKey(trimmedName))
            {
                outputName = _opNameTable[trimmedName];
                return true;
            }

            outputName = "";
            return false;
        }

        // TODO: external type classes
        private void _generateTypeClass(BlockNode node)
        {
            var tc = (CustomType)node.Nodes[0].Type;
            int buildType = 0;

            if (tc.Instances.Count == 1)
            {
                if (tc.Instances.First() is CustomNewType cnt)
                {
                    if (cnt.Values.Count == 1)
                        buildType = 1;
                    else if (cnt.Values.Count > 1)
                        buildType = 2;
                }                   
            }
            else
            {
                IEnumerable<int> cntFields = tc.Instances
                    .Where(x => x is CustomNewType)
                    .Select(x => ((CustomNewType)x).Values.Count);
                
                // if there are not new types, then we have a type union => type 1
                if (cntFields.Count() == 0 || cntFields.Max() == 1)
                    buildType = 1;
                else if (cntFields.Max() > 1)
                    buildType = 2;
            }

            if (buildType > 0)
            {
                string name = ((IdentifierNode)node.Nodes[0]).IdName;

                _table.Lookup(name, out Symbol symbol);
                name += _genericSuffix;

                bool exported = symbol.Modifiers.Contains(Modifier.EXPORTED);                

                var tcStruct = LLVM.StructCreateNamed(_ctx, exported ? _randPrefix + name : name);

                var valueHolder = LLVM.PointerType(LLVM.Int8Type(), 0);
                if (buildType == 2)
                    valueHolder = LLVM.PointerType(valueHolder, 0);

                tcStruct.StructSetBody(new[]
                {
                    LLVM.Int32Type(),
                    valueHolder
                }, true);

                _globalStructs[name] = tcStruct;
            }
        }

        private FnBodyBuilder _wrapBuilderFunc(BlockNode block, InternalBuilderAlgo ibo)
        {
            return delegate (LLVMValueRef vref)
            {
                return ibo(vref, block);
            };
        }
    }
}
