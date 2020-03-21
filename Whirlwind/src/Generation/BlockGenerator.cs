using System.Linq;
using System.Collections.Generic;

using LLVMSharp;

using Whirlwind.Semantic;

namespace Whirlwind.Generation
{
    partial class Generator
    {
        // bool says whether or not block contains a definite return (needs void or no)
        private bool _generateBlock(List<ITypeNode> block)
        {
            bool needsVoidTerminator = true;

            foreach (var node in block)
            {
                switch (node.Name)
                {
                    case "ExprReturn":
                        {
                            var ertNode = (StatementNode)node;

                            // build first arg for now
                            LLVM.BuildRet(_builder, _generateExpr(ertNode.Nodes[0]));

                            return false;
                        }
                    case "Return":
                        {
                            var rtNode = (StatementNode)node;

                            if (rtNode.Nodes.Count == 0)
                                LLVM.BuildRetVoid(_builder);
                            else if (rtNode.Nodes.Count == 1)
                                LLVM.BuildRet(_builder, _generateExpr(rtNode.Nodes[0]));
                            // tuples are reference types so we know they will be returned by an rt_val
                            else
                            {
                                var rtPtr = _getNamedValue("$rt_val").Vref;
                                var tuplePtr = _generateTupleLiteral(rtNode.Nodes);
                                _returnViaRtPtr(rtPtr, tuplePtr, rtNode.Nodes
                                    .Select(x => x.Type.SizeOf())
                                    .Aggregate((a, b) => a + b)
                                    );

                                return false;
                            }

                            return false;
                        }                      
                    case "ExprStmt":
                        _generateExpr(((StatementNode)node).Nodes[0]);
                        break;
                    case "DeclareVariable":
                        _generateVarDecl((StatementNode)node);
                        break;
                    case "DeclareConstant":
                    case "DeclareConstexpr":
                        _generateConstDecl((StatementNode)node);
                        break;
                    case "Assignment":
                        _generateAssignment((StatementNode)node);
                        break;
                    case "If":
                        _generateIfStatement((BlockNode)node); ;
                        break;
                    case "CompoundIf":
                        needsVoidTerminator &= _generateCompoundIf((BlockNode)node);
                        break;
                }
            }

            return needsVoidTerminator;
        }

        private void _returnViaRtPtr(LLVMValueRef rtPtr, LLVMValueRef valPtr, uint size)
        {
            var rti8Ptr = LLVM.BuildBitCast(_builder, rtPtr, _i8PtrType, "rt_i8ptr_tmp");
            var vali8Ptr = LLVM.BuildBitCast(_builder, valPtr, _i8PtrType, "val_i8ptr_tmp");

            LLVM.BuildCall(_builder, _globalScope["__memcpy"].Vref, 
                new[] { rti8Ptr, vali8Ptr, LLVM.ConstInt(LLVM.Int32Type(), size, new LLVMBool(0)) }, "");

            LLVM.BuildRetVoid(_builder);
        }
    }
}
