using System.Collections.Generic;
using System.Linq;

using LLVMSharp;

using Whirlwind.Semantic;
using Whirlwind.Types;

namespace Whirlwind.Generation
{
    partial class Generator
    {
        private void _generateStruct(BlockNode node, bool external)
        {
            var name = ((IdentifierNode)node.Nodes[0]).IdName + _genericSuffix;
            var st = (StructType)node.Nodes[0].Type;

            var llvmStruct = LLVM.StructCreateNamed(_ctx, name);
            _globalStructs[name] = llvmStruct; // forward declare struct

            llvmStruct.StructSetBody(st.Members.Select(x => _convertType(x.Value.DataType)).ToArray(), st.Packed);       

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

                    var llvmConstructor = _generateFunctionPrototype(name + suffix, cft, external);
                    _addGlobalDecl(name + suffix, llvmConstructor);

                    _appendFunctionBlock(llvmConstructor, new NoneType(), constructor);
                }
            }

            // add custom init members build algo and append
            var initFnProto = _generateFunctionPrototype(name + "._$initMembers", new FunctionType(new List<Parameter>
                            { _buildStructThisParam(st) },
                       new NoneType(), false), external);

            _addGlobalDecl(name + "._$initMembers", initFnProto);

            _appendFunctionBlock(initFnProto, new NoneType(), (initFn) =>
            {
                // build init members content
                for (int i = 0; i < st.Members.Count; i++)
                {
                    string memberName = st.Members.ElementAt(i).Key;
                    var memberValue = memberDict.ContainsKey(memberName) ?
                        _generateExpr(memberDict[memberName]) :
                        _getNullValue(st.Members.ElementAt(i).Value.DataType);

                    _setLLVMStructMember(_getNamedValue("this").Vref, memberValue, i, st.Members[memberName].DataType);
                }

                return true;
            });
        }

        private Parameter _buildStructThisParam(DataType dt)
            => new Parameter("this", dt, false, false, false, false);
    }
}
