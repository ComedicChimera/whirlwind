using System.Linq;

using Whirlwind.Types;

namespace Whirlwind.Semantic.Visitor
{
    partial class Visitor
    {
        private void _completeTree()
        {
            var block = ((BlockNode)_nodes[0]).Block;

            int scopePos = 0;
            foreach (var item in block)
            {
                switch (item.Name)
                {
                    case "Function":
                    case "AsyncFunction":
                        _completeFunction((BlockNode)item);
                        break;
                    case "Decorator":
                        _completeFunction((BlockNode)((BlockNode)item).Block[0]);
                        break;
                    case "TypeClass":
                        _completeTypeClass((BlockNode)item);
                        _table.MoveScope(_table.GetScopeCount(), scopePos);

                        scopePos++;
                        break;
                    case "Interface":
                        scopePos++;
                        break;
                    default:
                        continue;
                }
            }
        }

        private void _completeFunction(BlockNode fn)
        {
            if (fn.Block.Count == 1)
            {
                _nodes.Add(fn);

                _visitFunctionBody(((IncompleteNode)fn.Block[0]).AST, (FunctionType)fn.Nodes[0].Type);

                fn.Block = ((BlockNode)_nodes.Last()).Block;
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

        }
    }
}
