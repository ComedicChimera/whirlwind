using System;
using System.Collections.Generic;
using System.Text;

using Whirlwind.Semantic;

using LLVMSharp;

namespace Whirlwind.Generation
{
    partial class Generator
    {
        private void _generateIfStatement(BlockNode node)
        {
            _scopes.Add(new Dictionary<string, GeneratorSymbol>());

            LLVMValueRef ifExpr = _generateIfExpr(node);

            var thenBlock = LLVM.AppendBasicBlock(_currFunctionRef, "if_then");
            var endBlock = LLVM.AppendBasicBlock(_currFunctionRef, "if_end");

            LLVM.BuildCondBr(_builder, ifExpr, thenBlock, endBlock);

            LLVM.PositionBuilderAtEnd(_builder, thenBlock);
            if (_generateBlock(node.Block))
                LLVM.BuildBr(_builder, endBlock);

            LLVM.MoveBasicBlockAfter(endBlock, LLVM.GetLastBasicBlock(_currFunctionRef));
            LLVM.PositionBuilderAtEnd(_builder, endBlock);

            _scopes.RemoveLast();
        }

        private bool _generateCompoundIf(BlockNode node)
        {
            _scopes.Add(new Dictionary<string, GeneratorSymbol>());

            LLVMBasicBlockRef nextBlock;
            var endBlock = LLVM.AppendBasicBlock(_currFunctionRef, "if_end");

            int alternativeTerminatorCount = 0;
            for (int i = 0; i < node.Block.Count; i++)
            {
                var treeNode = (BlockNode)node.Block[i];

                if (treeNode.Name == "Else")
                {
                    if (_generateBlock(treeNode.Block))
                        LLVM.BuildBr(_builder, endBlock);
                    else
                        alternativeTerminatorCount++;

                    LLVM.PositionBuilderAtEnd(_builder, endBlock);
                }
                else
                {
                    var ifExpr = _generateIfExpr(treeNode);
                    var currBlock = LLVM.AppendBasicBlock(_currFunctionRef, treeNode.Name.ToLower() + "_then");

                    if (i == node.Block.Count - 1)
                        nextBlock = endBlock;
                    else if (node.Block[i + 1].Name == "Elif")
                        nextBlock = LLVM.AppendBasicBlock(_currFunctionRef, "elif_cond");
                    else
                        nextBlock = LLVM.AppendBasicBlock(_currFunctionRef, "else");

                    LLVM.BuildCondBr(_builder, ifExpr, currBlock, nextBlock);

                    LLVM.PositionBuilderAtEnd(_builder, currBlock);

                    if (_generateBlock(treeNode.Block))
                        LLVM.BuildBr(_builder, endBlock);
                    else
                        alternativeTerminatorCount++;

                    LLVM.PositionBuilderAtEnd(_builder, nextBlock);
                }                
            }

            // loop always ends with builder positioned at end block so no need to reposition

            // all blocks have alternative terminators, no need for end block
            if (alternativeTerminatorCount == node.Block.Count)
            {
                LLVM.RemoveBasicBlockFromParent(endBlock);
                _scopes.RemoveLast();
                return false;
            }               
            else
            {
                LLVM.MoveBasicBlockAfter(endBlock, LLVM.GetLastBasicBlock(_currFunctionRef));
                _scopes.RemoveLast();
                return true;
            }               
        }

        private LLVMValueRef _generateIfExpr(BlockNode node)
        {
            if (node.Nodes.Count == 2)
            {
                _generateVarDecl((StatementNode)node.Nodes[0]);
                return _generateExpr(node.Nodes[1]);
            }
            else
                return _generateExpr(node.Nodes[0]);
        }
    }
}
