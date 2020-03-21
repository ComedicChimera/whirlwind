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
                    cft.Parameters.Insert(0, _buildStructThisParam(st));

                    string suffix = ".constructor";

                    if (hasMultipleConstructors)
                        suffix += "." + string.Join(",", cft.Parameters.Select(x => x.DataType.LLVMName()));

                    var llvmConstructor = _generateFunctionPrototype(name + suffix, cft, exported);
                    _addGlobalDecl(name + suffix, llvmConstructor);

                    _appendFunctionBlock(llvmConstructor, new NoneType(), constructor);
                }
            }

            // add custom init members build algo and append
            var initFnProto = _generateFunctionPrototype(name + "._$initMembers", new FunctionType(new List<Parameter>
                            { _buildStructThisParam(st) },
                       new NoneType(), false), false);

            _addGlobalDecl(name + "._$initMembers", initFnProto);

            _appendFunctionBlock(initFnProto, new NoneType(), (initFn) =>
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

        private Parameter _buildStructThisParam(DataType dt)
            => new Parameter("this", dt, false, false, false, false);

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

                var valueHolder = _i8PtrType;
                if (buildType == 2)
                    valueHolder = LLVM.PointerType(valueHolder, 0);

                tcStruct.StructSetBody(new[]
                {
                    LLVM.Int16Type(),
                    valueHolder,
                    LLVM.Int32Type()
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
