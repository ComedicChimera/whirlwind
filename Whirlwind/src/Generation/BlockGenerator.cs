using System;
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
            foreach (var node in block)
            {
                switch (node.Name)
                {
                    case "ExprReturn":
                        {
                            var ertNode = (StatementNode)node;

                            // build first arg for now
                            LLVM.BuildRet(_builder, _generateExpr(ertNode.Nodes[0]));
                        }
                        break;
                    case "Return":
                        {
                            var rtNode = (StatementNode)node;

                            if (rtNode.Nodes.Count == 0)
                                LLVM.BuildRetVoid(_builder);
                            else
                            {
                                // just do 1 argument
                                var exprRes = _generateExpr(rtNode.Nodes[0]);

                                LLVM.BuildRet(_builder, exprRes);
                            }
                        }
                        break;
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
                }
            }

            return false;
        }
    }
}
