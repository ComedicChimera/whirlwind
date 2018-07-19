using Whirlwind.Parser;

using System;
using System.Collections.Generic;
using System.Linq;

namespace Whirlwind.Generator.Visitor
{
    class SemanticException : Exception
    {
        public readonly int Start, Length;
        public readonly new string Message;

        public SemanticException(string message, int start, int len)
        {
            Message = message;
            Start = start;
            Length = len;
        }
    }

    partial class Visitor
    {
        private List<ITypeNode> _nodes;
        private SymbolTable _table;

        public ITypeNode Visit(ASTNode ast)
        {
            foreach (INode node in ast.Content)
            {
                switch (node.Name())
                {
                    case "atom":
                        _visitAtom((ASTNode)node);
                        break;
                }
            }
            return _nodes[0];
        }

        private void MergeBack(int depth = 1)
        {
            depth++;
            for (int i = 1; i < depth; i++)
            {
                ((TreeNode)_nodes[_nodes.Count - depth]).Nodes.Add(_nodes[_nodes.Count - i]);
            }
        }

        public void PushForward(int depth = 1)
        {
            var ending = _nodes.Last();
            _nodes.RemoveAt(_nodes.Count - 1);
            var items = Enumerable.Range(_nodes.Count - (depth + 1), _nodes.Count - 1)
                .Select(i => _nodes[i]).Reverse().ToList();

            ((TreeNode)ending).Nodes.AddRange(items);

            _nodes.Add(ending);
        }
    }
}
