using Whirlwind.Parser;
using Whirlwind.Types;

using System.Collections.Generic;
using System.Linq;

namespace Whirlwind.Semantic.Visitor
{
    partial class Visitor
    {
        private List<ITypeNode> _nodes;
        private SymbolTable _table;

        public List<SemanticException> ErrorQueue;

        public Visitor()
        {
            _nodes = new List<ITypeNode>();
            ErrorQueue = new List<SemanticException>();
            _table = new SymbolTable();
        }

        public void Visit(ASTNode ast)
        {
            _nodes.Add(new BlockNode("Package"));

            foreach (INode node in ast.Content)
            {
                switch (node.Name)
                {
                    case "block_decl":
                        _visitBlockDecl((ASTNode)node, new List<Modifier>() { Modifier.CONSTANT });
                        break;
                    case "variable_decl":
                        _visitVarDecl((ASTNode)node, new List<Modifier>());
                        break;
                    case "export_decl":
                        ASTNode decl = (ASTNode)((ASTNode)node).Content[1];

                        switch (decl.Name)
                        {
                            case "block_decl":
                                _visitBlockDecl(decl, new List<Modifier>() { Modifier.EXPORTED, Modifier.CONSTANT });
                                break;
                            case "variable_decl":
                                _visitVarDecl(decl, new List<Modifier>() { Modifier.EXPORTED });
                                break;
                            case "include_stmt":
                                // add include logic
                                break;
                        }
                        break;
                    // add any logic surrounding inclusion
                    case "include_stmt":
                        break;
                    case "agent_decl":
                        _visitAgent((ASTNode)node);
                        break;
                    // catches rogue tokens and things of the like
                    default:
                        continue;
                }

                MergeToBlock();
            }

            _completeTree();
        }

        private void MergeBack(int depth = 1)
        {
            if (_nodes.Count <= depth)
                return;
            for (int i = 0; i < depth; i++)
            {
                ((TreeNode)_nodes[_nodes.Count - (depth - i + 1)]).Nodes.Add(_nodes[_nodes.Count - (depth - i)]);
                _nodes.RemoveAt(_nodes.Count - (depth - i));
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

            _nodes.Add((ITypeNode)ending);
        }

        private void PushToBlock()
        {
            BlockNode parent = (BlockNode)_nodes.Last();
            _nodes.RemoveAt(_nodes.Count - 1);

            parent.Block.Add(_nodes.Last());
            _nodes.RemoveAt(_nodes.Count - 1);

            _nodes.Add(parent);
        }

        private void MergeToBlock()
        {
            ITypeNode child = _nodes.Last();
            _nodes.RemoveAt(_nodes.Count - 1);

            ((BlockNode)_nodes.Last()).Block.Add(child);
        }

        private bool _isVoid(DataType type)
        {
            return type.Classify() == TypeClassifier.SIMPLE && ((SimpleType)type).Type == SimpleType.SimpleClassifier.VOID;
        }

        public ITypeNode Result() => _nodes.First();

        public SymbolTable Table()
        {
            return new SymbolTable(_table.Filter(s => s.Modifiers.Contains(Modifier.EXPORTED)));
        }
    }
}
