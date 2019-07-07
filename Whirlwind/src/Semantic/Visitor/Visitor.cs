using Whirlwind.Parser;
using Whirlwind.Types;

using System.Collections.Generic;
using System.Linq;
using System;

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

            var variableDecls = new List<Tuple<ASTNode, List<Modifier>>>();

            // visit block nodes first
            foreach (INode node in ast.Content)
            {
                switch (node.Name)
                {
                    case "block_decl":
                        _visitBlockDecl((ASTNode)node, new List<Modifier>());
                        break;
                    case "variable_decl":
                        variableDecls.Add(new Tuple<ASTNode, List<Modifier>>((ASTNode)node, new List<Modifier>()));
                        continue;
                    case "export_decl":
                        ASTNode decl = (ASTNode)((ASTNode)node).Content[1];

                        switch (decl.Name)
                        {
                            case "block_decl":
                                _visitBlockDecl(decl, new List<Modifier>() { Modifier.EXPORTED });
                                break;
                            case "variable_decl":
                                variableDecls.Add(new Tuple<ASTNode, List<Modifier>>(decl, new List<Modifier>() { Modifier.EXPORTED }));
                                continue;
                            case "include_stmt":
                                // add include logic
                                break;
                        }
                        break;
                    // add any logic surrounding inclusion
                    case "include_stmt":
                        break;
                    // catches rogue tokens and things of the like
                    default:
                        continue;
                }

                MergeToBlock();
            }

            // visit variable declaration second
            foreach (var varDecl in variableDecls)
            {
                _visitVarDecl(varDecl.Item1, varDecl.Item2);
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
            if (type.Classify() == TypeClassifier.VOID)
                return true;
            else if (type.Classify() == TypeClassifier.DICT)
            {
                var dictType = (DictType)type;

                return _isVoid(dictType.KeyType) || _isVoid(dictType.ValueType);
            }
            else if (type is IIterable)
                return _isVoid(((IIterable)type).GetIterator());
            else
                return false;
        }

        public ITypeNode Result() => _nodes.First();

        public SymbolTable Table()
        {
            return new SymbolTable(_table.Filter(s => s.Modifiers.Contains(Modifier.EXPORTED)));
        }
    }
}
