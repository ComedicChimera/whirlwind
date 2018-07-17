using System.Collections.Generic;
using Whirlwind.Parser;

namespace Whirlwind.Generator.Visitor
{
    partial class Visitor
    {
        private List<ITypeNode> _nodes;

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
    }
}
