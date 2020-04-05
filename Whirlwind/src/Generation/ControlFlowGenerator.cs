using System;
using System.Collections.Generic;
using System.Linq;

using Whirlwind.Semantic;
using Whirlwind.Types;

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

        private bool _generateForInfinite(BlockNode node)
        {
            _scopes.Add(new Dictionary<string, GeneratorSymbol>());

            var contLabel = LLVM.AppendBasicBlock(_currFunctionRef, "for_inf_body");
            var breakLabel = LLVM.AppendBasicBlock(_currFunctionRef, "for_inf_end");

            var prevLCtx = _saveAndUpdateLoopContext(breakLabel, contLabel);

            LLVM.BuildBr(_builder, contLabel);

            LLVM.PositionBuilderAtEnd(_builder, contLabel);
            bool needsTerminator = _generateBlock(node.Block);

            if (needsTerminator)
                LLVM.BuildBr(_builder, contLabel);

            if (_breakLabelUsed)
            {
                LLVM.MoveBasicBlockAfter(breakLabel, LLVM.GetLastBasicBlock(_currFunctionRef));
                LLVM.PositionBuilderAtEnd(_builder, breakLabel);
                needsTerminator = true;
            }            
            else
            {
                LLVM.RemoveBasicBlockFromParent(breakLabel);
                needsTerminator = false;
            }
                
            _restoreLoopContext(prevLCtx);
            _scopes.RemoveLast();

            return needsTerminator;
        }

        private bool _generateForCond(BlockNode node)
        {
            _scopes.Add(new Dictionary<string, GeneratorSymbol>());

            var condLabel = LLVM.AppendBasicBlock(_currFunctionRef, "for_cond");
            LLVM.BuildBr(_builder, condLabel);

            LLVM.PositionBuilderAtEnd(_builder, condLabel);

            var bodyLabel = LLVM.AppendBasicBlock(_currFunctionRef, "for_body");
            var endLabel = LLVM.AppendBasicBlock(_currFunctionRef, "for_end");

            var condExpr = _generateIfExpr(node);
            var lCtx = _saveAndUpdateLoopContext(endLabel, condLabel);

            bool afterReachesEnd = true;
            if (node.Block.LastOrDefault()?.Name == "After")
            {
                var afterLabel = LLVM.AppendBasicBlock(_currFunctionRef, "for_after");
                LLVM.BuildCondBr(_builder, condExpr, bodyLabel, afterLabel);

                LLVM.PositionBuilderAtEnd(_builder, afterLabel);

                if (_generateBlock(((BlockNode)node.Block.Last()).Block))
                    LLVM.BuildBr(_builder, endLabel);
                else if (!_breakLabelUsed)
                    afterReachesEnd = false;
            }
            else
                LLVM.BuildCondBr(_builder, condExpr, bodyLabel, endLabel);

            LLVM.PositionBuilderAtEnd(_builder, bodyLabel);

            // after block is not evaluated in the main generate block function so we don't need to trim it
            if (_generateBlock(node.Block))
                LLVM.BuildBr(_builder, condLabel);

            bool hasExitCond = _breakLabelUsed || afterReachesEnd;
            if (hasExitCond)
            {
                LLVM.MoveBasicBlockAfter(endLabel, LLVM.GetLastBasicBlock(_currFunctionRef));
                LLVM.PositionBuilderAtEnd(_builder, endLabel);
            }
            else
                LLVM.RemoveBasicBlockFromParent(endLabel);

            _restoreLoopContext(lCtx);     

            _scopes.RemoveLast();

            return hasExitCond;
        }

        private bool _generateCFor(BlockNode node)
        {
            _scopes.Add(new Dictionary<string, GeneratorSymbol>());

            LLVMBasicBlockRef exitLabel, contLabel, beginLabel, bodyLabel, endLabel;
            bodyLabel = LLVM.AppendBasicBlock(_currFunctionRef, "for_body");
            endLabel = LLVM.AppendBasicBlock(_currFunctionRef, "for_end");

            beginLabel = bodyLabel;
            contLabel = bodyLabel;

            bool hasCondition = false, hasUpdate = false, afterReachesEnd = false;
            bool hasAfter = node.Block.LastOrDefault()?.Name == "After";

            if (hasAfter)
                exitLabel = LLVM.AppendBasicBlock(_currFunctionRef, "for_after");
            else
                exitLabel = endLabel;

            foreach (var item in ((ExprNode)node.Nodes[0]).Nodes)
            {
                var enode = (ExprNode)item;

                switch (item.Name)
                {
                    case "IterVarDecl":
                        {
                            var id = (IdentifierNode)enode.Nodes[0];

                            var iterVarPtr = LLVM.BuildAlloca(_builder, _convertType(id.Type, true), id.IdName);
                            // no need to cast check here since iterator variables can't have type specifiers
                            LLVM.BuildStore(_builder, _generateExpr(enode.Nodes[1]), iterVarPtr);

                            _setVar(id.IdName, iterVarPtr, true);
                        }
                        break;
                    case "CForCondition":
                        {
                            hasCondition = true;

                            beginLabel = LLVM.AppendBasicBlock(_currFunctionRef, "for_cond");
                            LLVM.MoveBasicBlockBefore(beginLabel, bodyLabel); // move the condition label before the body label

                            LLVM.BuildBr(_builder, beginLabel);

                            LLVM.PositionBuilderAtEnd(_builder, beginLabel);

                            var condExpr = _generateExpr(enode.Nodes[0]);
                            LLVM.BuildCondBr(_builder, condExpr, bodyLabel, exitLabel);
                        }
                        break;
                    case "CForUpdateExpr":
                        hasUpdate = true;

                        contLabel = LLVM.AppendBasicBlock(_currFunctionRef, "for_update");
                        LLVM.MoveBasicBlockBefore(contLabel, bodyLabel);

                        LLVM.PositionBuilderAtEnd(_builder, contLabel);
                        _generateExpr(enode.Nodes[0]);

                        LLVM.BuildBr(_builder, beginLabel);
                        break;
                    case "CForUpdateAssignment":
                        hasUpdate = true;

                        contLabel = LLVM.AppendBasicBlock(_currFunctionRef, "for_update");
                        LLVM.MoveBasicBlockBefore(contLabel, bodyLabel);

                        LLVM.PositionBuilderAtEnd(_builder, contLabel);
                        _generateAssignment((StatementNode)enode.Nodes[0]);

                        LLVM.BuildBr(_builder, beginLabel);
                        break;
                }
            }

            // make sure it is positioned to the new begin label (even if it is still pointed to
            // body label, b/c it might not be) if necessary (ie. it has not been repositioned to update label)
            if (!hasUpdate)
                contLabel = beginLabel;

            var lCtx = _saveAndUpdateLoopContext(endLabel, contLabel);

            LLVM.PositionBuilderAtEnd(_builder, bodyLabel);
            bool bodyNeedsTerminator = _generateBlock(node.Block);

            // see note on after block in for cond generation
            if (bodyNeedsTerminator)
                LLVM.BuildBr(_builder, contLabel);
            // get rid of update block if not used
            else if (!_continueLabelUsed && hasUpdate)
                LLVM.RemoveBasicBlockFromParent(contLabel);

            if (hasAfter) {
                if (!hasCondition)
                {
                    hasAfter = false;
                    LLVM.RemoveBasicBlockFromParent(exitLabel);
                }
                else
                {
                    LLVM.MoveBasicBlockAfter(exitLabel, LLVM.GetLastBasicBlock(_currFunctionRef));
                    LLVM.PositionBuilderAtEnd(_builder, exitLabel);

                    afterReachesEnd = _generateBlock(((BlockNode)node.Block.Last()).Block);

                    if (afterReachesEnd)
                        LLVM.BuildBr(_builder, endLabel);
                }                
            }

            bool keepEndLabel = true;

            // hasAfter => hasCondition
            if (hasAfter)
                keepEndLabel = _breakLabelUsed || afterReachesEnd;
            else
                keepEndLabel = _breakLabelUsed || hasCondition;

            if (!hasCondition)
            {
                LLVM.PositionBuilderAtEnd(_builder, LLVM.GetPreviousBasicBlock(beginLabel));
                LLVM.BuildBr(_builder, beginLabel);
            }

            if (keepEndLabel)
            {
                LLVM.MoveBasicBlockAfter(endLabel, LLVM.GetLastBasicBlock(_currFunctionRef));
                LLVM.PositionBuilderAtEnd(_builder, endLabel);
            }
            else
                LLVM.RemoveBasicBlockFromParent(endLabel);

            _scopes.RemoveLast();
            _restoreLoopContext(lCtx);

            return keepEndLabel;
        }

        private bool _generateForIter(BlockNode node)
        {
            var beginBlock = LLVM.AppendBasicBlock(_currFunctionRef, "for_iter_update");
            LLVM.BuildBr(_builder, beginBlock);

            var bodyBlock = LLVM.AppendBasicBlock(_currFunctionRef, "for_iter_body");
            var endBlock = LLVM.AppendBasicBlock(_currFunctionRef, "for_iter_end");

            LLVMBasicBlockRef exitBlock;
            bool exitHasTerminator = false;
            if (node.Block.Last().Name == "After")
            {
                exitBlock = LLVM.AppendBasicBlock(_currFunctionRef, "for_iter_after");
                LLVM.PositionBuilderAtEnd(_builder, exitBlock);

                if (_generateBlock(((BlockNode)node.Block.Last()).Block))
                    LLVM.BuildBr(_builder, endBlock);
                else
                    exitHasTerminator = true;
            }
            else
                exitBlock = endBlock;

            _scopes.Add(new Dictionary<string, GeneratorSymbol>());

            LLVM.PositionBuilderAtEnd(_builder, beginBlock);
            var setupBlock = _buildIteratorUpdateBlock(_setupIterator((ExprNode)node.Nodes[0]), bodyBlock, exitBlock);
            LLVM.MoveBasicBlockAfter(setupBlock, beginBlock);

            LLVM.PositionBuilderAtEnd(_builder, bodyBlock);
            var lCtx = _saveAndUpdateLoopContext(endBlock, beginBlock);

            if (_generateBlock(node.Block))
                LLVM.BuildBr(_builder, beginBlock);

            bool needsTerminator = true;
            if (exitHasTerminator && !_breakLabelUsed)
            {
                LLVM.RemoveBasicBlockFromParent(endBlock);
                needsTerminator = false;
            }               
            else
            {
                LLVM.MoveBasicBlockAfter(endBlock, LLVM.GetLastBasicBlock(_currFunctionRef));
                LLVM.PositionBuilderAtEnd(_builder, endBlock);
            }

            _restoreLoopContext(lCtx);
            _scopes.RemoveLast();

            return needsTerminator;
        }

        private bool _generateSelect(BlockNode node)
        {
            var selectExprNode = node.Nodes[0];
            var selectType = selectExprNode.Type;

            var caseBlocks = node.Block
                .Where(x => x.Name != "Default")
                .Select(x => LLVM.AppendBasicBlock(_currFunctionRef, "case"))
                .ToList();

            var defaultBlock = LLVM.AppendBasicBlock(_currFunctionRef, "default");

            bool allBlocksHaveAlternativeTerminators;
            var caseScopes = new List<Dictionary<string, GeneratorSymbol>>();
            if (_isPatternType(selectType))
                allBlocksHaveAlternativeTerminators = _generatePatternMatch(selectExprNode, node.Block
                    .Select(x => (BlockNode)x).Where(x => x.Name == "Case").ToList(),
                    caseBlocks, defaultBlock, out caseScopes);
            // is standard switch case (no pattern matching)
            else
            {
                var onValNodes = node.Block
                    .Take(caseBlocks.Count)
                    .SelectMany(x => ((BlockNode)x).Nodes)
                    .Select(x => _getSwitchableValue(x, selectType))
                    .ToList();

                var selectExpr = _getSwitchableValue(selectExprNode, selectType);

                _switchBetween(selectExpr, onValNodes, caseBlocks, defaultBlock);

                allBlocksHaveAlternativeTerminators = true;
            }

            var selectEnd = LLVM.AppendBasicBlock(_currFunctionRef, "select_end");

            bool generatedDefaultBlock = false;
            for (int i = 0; i < node.Block.Count; i++)
            {
                if (caseScopes.Count == 0)
                    _scopes.Add(new Dictionary<string, GeneratorSymbol>());
                else
                    _scopes.Add(caseScopes[i]);

                var block = (BlockNode)node.Block[i];

                // default block
                if (i == caseBlocks.Count)
                {
                    generatedDefaultBlock = true;
                    LLVM.PositionBuilderAtEnd(_builder, defaultBlock);

                    if (_generateBlock(block.Block))
                    {
                        LLVM.BuildBr(_builder, selectEnd);
                        allBlocksHaveAlternativeTerminators = false;
                    }                       
                }
                else
                {
                    LLVM.PositionBuilderAtEnd(_builder, caseBlocks[i]);

                    if (_generateBlock(block.Block))
                    {
                        LLVM.BuildBr(_builder, selectEnd);
                        allBlocksHaveAlternativeTerminators = false;
                    }
                }

                _scopes.RemoveLast();
            }

            if (!generatedDefaultBlock)
            {
                LLVM.PositionBuilderAtEnd(_builder, defaultBlock);
                LLVM.BuildBr(_builder, selectEnd);
                allBlocksHaveAlternativeTerminators = false;
            }

            if (allBlocksHaveAlternativeTerminators)
                LLVM.RemoveBasicBlockFromParent(selectEnd);
            else
                LLVM.PositionBuilderAtEnd(_builder, selectEnd);

            return allBlocksHaveAlternativeTerminators;
        }
    }
}
