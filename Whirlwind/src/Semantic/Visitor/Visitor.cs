using Whirlwind.Parser;
using Whirlwind.Types;

using System.Collections.Generic;
using System.Linq;
using System;

namespace Whirlwind.Semantic.Visitor
{
    // The Visitor Class
    // Responsible for performing the
    // bulk of semantic analysis and
    // constructing the Semantic Action Tree
    partial class Visitor
    {
        // ------------
        // Visitor Data
        // ------------

        // exposed list of all semantic exceptions accumulated
        // during visitation; processed outside of visitor
        public List<SemanticException> ErrorQueue;

        // visitor shared data
        private List<ITypeNode> _nodes;
        private SymbolTable _table;
        private string _namePrefix;      
        private CustomType _typeClassContext;

        // visitor state flags
        private bool _couldLambdaContextExist = false, _couldTypeClassContextExist = false;
        private bool _didTypeClassCtxInferFail = false;
        private bool _isTypeCast = false;
        private bool _isGenericSelfContext = false;

        public Visitor(string namePrefix)
        {
            _nodes = new List<ITypeNode>();
            _table = new SymbolTable();
            _namePrefix = namePrefix;

            ErrorQueue = new List<SemanticException>();
        }

        // ----------------------
        // Main Visitor Functions
        // ----------------------

        // main visit function
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

        // returns final, visited node
        public ITypeNode Result() => _nodes.First();

        // returns export table
        public SymbolTable Table()
        {
            return new SymbolTable(_table.Filter(s => s.Modifiers.Contains(Modifier.EXPORTED)));
        }

        // ------------------------------------
        // Construction Stack Control Functions
        // ------------------------------------

        // moves all the ending nodes on the CS stack into
        // the node at the specified depth in order
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

        // moves all the nodes preceding the ending node
        // as far back as the specified depth on the CS stack 
        // into the ending node in order
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

        // moves the node preceding the ending node
        // into the ending node's block
        private void PushToBlock()
        {
            BlockNode parent = (BlockNode)_nodes.Last();
            _nodes.RemoveAt(_nodes.Count - 1);

            parent.Block.Add(_nodes.Last());
            _nodes.RemoveAt(_nodes.Count - 1);

            _nodes.Add(parent);
        }

        // moves the ending node into the preceding
        // node's block
        private void MergeToBlock()
        {
            ITypeNode child = _nodes.Last();
            _nodes.RemoveAt(_nodes.Count - 1);

            ((BlockNode)_nodes.Last()).Block.Add(child);
        }

        // -------------------------------------------
        // Context-Based/Inductive Inferencing Helpers
        // -------------------------------------------

        // sets type class context label and calls check for setting
        // lambda context label
        private void _addContext(ASTNode node)
        {
            _couldTypeClassContextExist = true;

            _addLambdaContext(node);
        }

        // checks if lambda context could exist and sets appropriate label if it can
        private void _addLambdaContext(ASTNode node)
        {
            if (node.Name == "lambda")
                _couldLambdaContextExist = true;
            else if (node.Content.Count == 1 && node.Content[0] is ASTNode anode)
                _addLambdaContext(anode);
        }

        // applies context if possible, errors if not
        private void _giveContext(IncompleteNode inode, DataType ctx)
        {
            if (ctx is FunctionType fctx)
                _visitLambda(inode.AST, fctx);
            else if (ctx is CustomType tctx)
            {
                _typeClassContext = tctx;

                _visitExpr(inode.AST);

                _typeClassContext = null;
            }
            else
            {
                string errorMsg;

                if (_didTypeClassCtxInferFail)
                {
                    errorMsg = "Unable to infer type class context for expression";
                    _didTypeClassCtxInferFail = false;
                }
                else
                    errorMsg = "Unable to infer type(s) of lambda arguments";

                throw new SemanticException(errorMsg, inode.AST.Position);
            }
        }

        // sets both context-indefinence variables to false
        private void _clearPossibleContext()
        {
            _couldLambdaContextExist = false;
            _couldTypeClassContextExist = false;
        }

        // -------------------
        // Utility Function(s)
        // -------------------

        // checks to see if a type is void
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
    }
}
