using Whirlwind.Parser;
using Whirlwind.Types;

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
                        _visitBlockDecl((ASTNode)node, new List<Modifier>());
                        break;
                    case "variable_decl":
                        _visitVarDecl((ASTNode)node, new List<Modifier>());
                        break;
                    case "export_decl":
                        ASTNode decl = (ASTNode)((ASTNode)node).Content[1];

                        switch (decl.Name)
                        {
                            case "block_decl":
                                _visitBlockDecl(decl, new List<Modifier>() { Modifier.EXPORTED });
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
                    // catches rogue tokens and things of the like
                    default:
                        continue;
                }

                MergeToBlock();
            }

            _completeTree();
        }

        private void _completeTree()
        {
            var block = ((BlockNode)_nodes[0]).Block;

            uint scopePos = 0;
            for (int i = 0; i < block.Count; i++)
            {
                var item = block[i];

                switch (item.Name)
                {
                    case "Function":
                    case "AsyncFunction":
                        {
                            BlockNode fn = (BlockNode)item;

                            if (fn.Block.Count == 1)
                            {
                                _nodes.Add(fn);

                                _visitFunctionBody(((IncompleteNode)fn.Block[0]).AST, (FunctionType)fn.Nodes[0].Type);
                                fn.Block.RemoveAt(0);

                                ((BlockNode)_nodes[0]).Block[i] = _nodes.Last();
                                _nodes.RemoveAt(1);
                            }
                        }
                        break;
                    case "Decorator":
                        {
                            BlockNode fn = (BlockNode)((BlockNode)item).Block[0];

                            if (fn.Block.Count == 1)
                            {
                                _nodes.Add(fn);

                                _visitFunctionBody(((IncompleteNode)fn.Block[0]).AST, (FunctionType)fn.Nodes[0].Type);
                                fn.Block.RemoveAt(0);

                                ((BlockNode)((BlockNode)_nodes[0]).Block[i]).Block[0] = _nodes.Last();
                                _nodes.RemoveAt(1);
                            }
                        }
                        break;
                    case "TypeClass":
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

        private bool _isVoid(IDataType type)
        {
            return type.Classify() == TypeClassifier.SIMPLE && ((SimpleType)type).Type == SimpleType.DataType.VOID;
        }

        public ITypeNode Result() => _nodes.First();

        public SymbolTable Table()
        {
            return new SymbolTable(_table.Filter(s => s.Modifiers.Contains(Modifier.EXPORTED)));
        }
    }
}
