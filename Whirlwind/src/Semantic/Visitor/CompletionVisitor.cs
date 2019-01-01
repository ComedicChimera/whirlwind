using System.Linq;
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
                        _table.GotoScope(scopePos);

                        int tPos = 0;
                        _completeBlock(((BlockNode)node).Block[0], ref tPos);

                        _table.AscendScope();
                    }

                    scopePos++;
                    break;
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
    }
}
