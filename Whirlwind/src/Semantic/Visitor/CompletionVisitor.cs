﻿using System.Linq;
using System.Collections.Generic;

using Whirlwind.Types;

namespace Whirlwind.Semantic.Visitor
{
    partial class Visitor
    {
        private void _completeTree()
        {
            var block = ((BlockNode)_nodes[0]).Block;

            int scopePos = 0;
            foreach (var node in block)
                _completeBlock(node, ref scopePos);

            _evaluateGenerates(block);
        }

        private void _completeBlock(ITypeNode node, ref int scopePos)
        {
            switch (node.Name)
            {
                case "Function":
                case "AsyncFunction":
                    _completeFunction((BlockNode)node);
                    break;
                case "Decorator":
                    _completeFunction((BlockNode)((BlockNode)node).Block[0]);

                    scopePos++;
                    break;
                case "TypeClass":
                    _completeTypeClass((BlockNode)node);
                    _table.MoveScope(_table.GetScopeCount() - 1, scopePos);

                    scopePos++;
                    break;
                case "Interface":
                    _completeInterface((BlockNode)node);
                    _table.MoveScope(_table.GetScopeCount() - 1, scopePos);

                    scopePos++;
                    break;
                case "Template":
                    {
                        // visit normal template body and generate necessary code
                        _table.GotoScope(scopePos);

                        var template = ((BlockNode)node);

                        int tPos = 0;
                        _completeBlock(template.Block[0], ref tPos);

                        _table.AscendScope();
                    }

                    scopePos++;
                    break;
                case "Variant":
                    // variants are basically functions for this stage of compilation
                    _completeFunction((BlockNode)node);
                    break;
                // increment scope pos on struct
                case "Struct":
                    scopePos++;
                    break;
            }
        }

        private void _evaluateGenerates(List<ITypeNode> block)
        {
            int scopePos = 0;

            foreach (var node in block)
            {
                switch (node.Name)
                {
                    case "Decorator":
                    case "Function":
                    case "AsyncFunction":
                        scopePos++;
                        break;
                    case "Template":
                        {
                            // perform generate checking (clean up scope and nodes after done)
                            // create working scope for clean template generate checking
                            _table.AddScope();
                            _table.DescendScope();

                            int tPos = 0;

                            foreach (var generate in ((TemplateType)((BlockNode)node).Nodes[0].Type).Generates)
                            {
                                // create artificial scope for all who need it
                                if (new[] { "TypeClass", "Interface", "Decorator" }.Contains(generate.Block.Name))
                                    _table.AddScope();

                                int refTemp = tPos;
                                _completeBlock(generate.Block, ref refTemp);

                                tPos++;
                            }

                            // clean up scope
                            _table.AscendScope();
                            _table.RemoveScope();
                        }

                        scopePos++;
                        break;
                    case "TypeClass":
                    case "Interface":
                        _table.GotoScope(scopePos);

                        _evaluateGenerates(((BlockNode)node).Block);

                        _table.AscendScope();

                        scopePos++;
                        break;
                    case "Agent":
                        _table.GotoScope(scopePos);

                        _completeAgent((BlockNode)node);

                        _table.AscendScope();

                        scopePos++;
                        break;
                }
            }
        }

        private void _completeFunction(BlockNode fn)
        {
            if (fn.Block.Count == 1)
            {
                _nodes.Add(fn);

                try
                {
                    _visitFunctionBody(((IncompleteNode)fn.Block[0]).AST, (FunctionType)fn.Nodes[0].Type);

                    fn.Block = ((BlockNode)_nodes.Last()).Block;
                    fn.Block.RemoveAt(0); // remove incomplete node lol
                }
                catch (SemanticException se)
                {
                    ErrorQueue.Add(se);

                    fn.Block = new List<ITypeNode>();
                }
                
                _nodes.RemoveAt(1);
            }
        }

        private void _completeTypeClass(BlockNode typeClass)
        {
            _table.AddScope();
            _table.DescendScope();

            _table.AddSymbol(new Symbol("$THIS", ((ObjectType)typeClass.Nodes[0].Type).GetInternalInstance()));

            foreach (var item in typeClass.Block)
            {
                if (item.Name.EndsWith("Function"))
                    _completeFunction((BlockNode)item);
                else if (item.Name == "Constructor")
                {
                    _nodes.Add(item);

                    BlockNode c = (BlockNode)item;

                    _visitFunctionBody(((IncompleteNode)c.Block[0]).AST, (FunctionType)c.Nodes[0].Type);

                    c.Block = ((BlockNode)_nodes.Last()).Block;
                    _nodes.RemoveAt(1);
                }
            }

            _table.AscendScope();
        }

        private void _completeInterface(BlockNode interf)
        {
            _table.AddScope();
            _table.DescendScope();

            _table.AddSymbol(new Symbol("$THIS", ((InterfaceType)interf.Nodes[0].Type).GetInstance()));

            foreach (var item in interf.Block)
            {
                if (item.Name.EndsWith("Function"))
                    _completeFunction((BlockNode)item);
            }

            _table.AscendScope();
        }

        private void _completeAgent(BlockNode block)
        {
            foreach (var item in block.Block)
            {
                if (item.Name == "EventHandler")
                {
                    _table.AddScope();
                    _table.DescendScope();

                    BlockNode evNode = (BlockNode)item;

                    // first symbol declared in scope => no check required
                    _table.AddSymbol(new Symbol(((IdentifierNode)evNode.Nodes[1]).IdName, evNode.Nodes[0].Type, 
                        new List<Modifier> { Modifier.CONSTANT }));

                    _nodes.Add(new BlockNode("EventBlock"));
                    _visitBlockNode(((IncompleteNode)evNode.Block[0]).AST, new StatementContext(true, false, false));

                    evNode.Block = ((BlockNode)_nodes.Last()).Block;
                    _nodes.RemoveAt(_nodes.Count - 1);

                    _table.AscendScope();
                }
                else if (item.Name.EndsWith("Function"))
                    _completeFunction((BlockNode)item);
            }
        }
    }
}
