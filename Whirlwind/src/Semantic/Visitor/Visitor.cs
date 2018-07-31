using Whirlwind.Parser;

using System;
using System.Collections.Generic;
using System.Linq;

namespace Whirlwind.Semantic.Visitor
{
    class SemanticException : Exception
    {
        public readonly TextPosition Position;
        public readonly new string Message;

        public SemanticException(string message, TextPosition pos)
        {
            Message = message;
            Position = pos;
        }
    }

    partial class Visitor
    {
        private List<ITypeNode> _nodes;
        private SymbolTable _table;

        public Visitor()
        {
            _nodes = new List<ITypeNode>();
            _table = new SymbolTable();
        }

        public void Visit(ASTNode ast)
        {
            foreach (INode node in ast.Content)
            {
                switch (node.Name)
                {
                    case "atom":
                        _visitAtom((ASTNode)node);
                        break;
                    // prevent tokens from being recognized
                    case "TOKEN":
                        break;       
                    default:
                        Visit((ASTNode)node);
                        break;
                }
            }
        }

        private void MergeBack(int depth = 1)
        {
            if (_nodes.Count <= depth)
                return;
            for (int i = 0; i < depth; i++)
            {
                ((TreeNode)_nodes[_nodes.Count - (depth + 1)]).Nodes.Add(_nodes[_nodes.Count - 1]);
                _nodes.RemoveAt(_nodes.Count - 1);
            }
        }

        private void PushForward(int depth = 1)
        {
            if (_nodes.Count <= depth)
                return;
            var ending = (TreeNode)_nodes.Last();
            _nodes.RemoveAt(_nodes.Count - 1);

            for (int i = 0; i < depth; i++)
            {
                int pos = _nodes.Count - (depth - i);
                ending.Nodes.Add(_nodes[pos]);
                _nodes.RemoveAt(pos);
            }

            _nodes.Add(ending);
        }

        public ITypeNode Result() => _nodes.First();
    }
}
