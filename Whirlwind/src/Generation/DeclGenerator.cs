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
            llvmStruct.StructSetBody(st.Members.Select(x => _convertType(x.Value.DataType)).ToArray(), packed);

            _globalStructs[name] = llvmStruct;

            var memberDict = new Dictionary<string, ITypeNode>();
            bool needsInitMembers = false;

            // process constructor
            bool hasMultipleConstructors = st.HasMultipleConstructors();
            foreach (var item in node.Block)
            {
                if (item is ExprNode eNode)
                {
                    if (memberDict.Count == 0)
                        needsInitMembers = true;

                    foreach (var elem in eNode.Nodes.Skip(1))
                        memberDict.Add(((IdentifierNode)elem).IdName, eNode.Nodes[0]);
                }
                else
                {
                    BlockNode constructor = (BlockNode)item;
                    FunctionType cft = (FunctionType)(constructor.Nodes[0].Type);
                    cft.Parameters.Insert(0, _buildThisParameter(st));

                    string suffix = ".constructor";

                    if (hasMultipleConstructors)
                        suffix += "." + string.Join(",", cft.Parameters.Select(x => x.DataType.LLVMName()));

                    var llvmConstructor = _generateFunctionPrototype(name + suffix, cft, exported);
                    _globalScope[name + suffix] = llvmConstructor;

                    if (constructor.Block.Count > 0)
                        _appendFunctionBlock(llvmConstructor, constructor);
                }
            }

            if (needsInitMembers)
            {
                // add custom init members build algo and append
                var initFn = _generateFunctionPrototype(name + "._$initMembers", new FunctionType(new List<Parameter>
                            { _buildThisParameter(st) },
                           new NoneType(), false), false);

                _declareFnArgs(initFn);

                LLVM.PositionBuilderAtEnd(_builder, LLVM.AppendBasicBlockInContext(_ctx, initFn, "entry"));

                // build init members content
                for (int i = 0; i < st.Members.Count; i++)
                {
                    var memberPtr = LLVM.BuildStructGEP(_builder, _getNamedValue("this"), (uint)i, "get_member_tmp");

                    string memberName = st.Members.ElementAt(i).Key;
                    var memberValue = memberDict.ContainsKey(memberName) ? 
                        _generateExpr(memberDict[memberName]) : 
                        _getNullValue(st.Members.ElementAt(i).Value.DataType);

                    LLVM.BuildStore(_builder, memberValue, memberPtr);
                }

                LLVM.BuildRetVoid(_builder);

                _scopes.RemoveLast();

                _globalScope[name + "._$initMembers"] = initFn;

                LLVM.VerifyFunction(initFn, LLVMVerifierFailureAction.LLVMPrintMessageAction);
            }
        }

        private Parameter _buildThisParameter(DataType dt)
            => new Parameter("this", new PointerType(dt, false), false, false, false, false);

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
                // build method if necessary
                if (method.Key.DataType is FunctionType fnType)
                {
                    // check for operator overloads

                    methods.Add(_convertType(fnType));

                    if (method.Value)
                    {
                        var llvmMethod = _generateFunctionPrototype(name + "." + method.Key.Name, fnType, exported);

                        _appendFunctionBlock(llvmMethod, ((BlockNode)node.Block[methodNdx]));
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
                LLVM.Int16Type(),
                vtableStruct
            }, false);

            _globalStructs[name] = interfStruct;
        }

        private void _generateTypeClass(BlockNode node)
        {
            var tc = (CustomType)(node.Nodes[0].Type);
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
