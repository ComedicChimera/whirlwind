using System.Linq;
using System.Collections.Generic;

using Whirlwind.Types;
using Whirlwind.Syntax;

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
            if (node.Name.StartsWith("$FILE_NAME$"))
            {
                _fileName = node.Name.Substring(11);
                return;
            }

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
                case "Interface":
                case "BindInterface":
                case "BindGenerateInterface":
                    _completeInterface((BlockNode)node, true);
                    _table.MoveScope(_table.GetScopeCount() - 1, scopePos);

                    // complete all sub generics (avoid scope complications)
                    _completeGenerics((BlockNode)node, scopePos);

                    scopePos++;
                    break;
                case "Generic":
                    {
                        _table.GotoScope(scopePos);

                        int _ = 0;
                        _completeBlock(((BlockNode)node).Block[0], ref _);

                        _table.AscendScope();
                    }

                    scopePos++;
                    break;
                case "BindGenericInterface":
                    {
                        _table.AddScope();
                        _table.DescendScope();

                        foreach (var name in ((GenericType)((TreeNode)node).Nodes[0].Type).GenericVariables.Select(x => x.Name))
                            _table.AddSymbol(new Symbol(name, new GenericPlaceholder(name)));

                        _completeInterface((BlockNode)node, false);
                        _table.MoveScope(_table.GetScopeCount() - 1, scopePos);

                        // complete all sub generics (avoid scope complications)
                        _completeGenerics((BlockNode)node, scopePos);

                        scopePos++;
                    }
                    break;
                case "Variant":
                    // variants are basically functions for this stage of compilation
                    _completeFunction((BlockNode)node);
                    break;
                case "Struct":
                    {
                        _table.GotoScope(scopePos);

                        IdentifierNode idNode = (IdentifierNode)((BlockNode)node).Nodes[0];

                        _table.AddSymbol(new Symbol(idNode.Name, ((StructType)idNode.Type).GetInstance().ConstCopy()));

                        _table.AddSymbol(new Symbol("$THIS", ((StructType)idNode.Type).GetInstance()));

                        foreach (var item in ((BlockNode)node).Block)
                        {
                            if (item.Name == "Constructor")
                                _completeFunction((BlockNode)item);
                            else if (item.Name == "MemberInitializer")
                            {
                                var initializer = (ExprNode)item;
                                var iAst = ((IncompleteNode)initializer.Nodes[0]).AST;

                                // this function handles all errors :D
                                if (_completeExprWithCtx(iAst, item.Type, out ITypeNode res))
                                    initializer.Nodes[0] = res;
                            }
                        }

                        _table.AscendScope();
                    }

                    scopePos++;
                    break;
                case "TypeClass":
                    scopePos++;
                    break;
                case "AnnotatedBlock":
                    {
                        BlockNode anode = (BlockNode)node;
                        StatementNode annotation = (StatementNode)anode.Block[0];

                        if (((ValueNode)annotation.Nodes[0]).Value == "friend")
                            _friendName = ((ValueNode)annotation.Nodes[1]).Value;

                        _completeBlock((BlockNode)anode.Block[1], ref scopePos);

                        if (_friendName != "")
                            _friendName = "";
                    }
                    break;
            }
        }        

        private void _evaluateGenerates(List<ITypeNode> block)
        {
            int scopePos = 0;

            foreach (var node in block)
            {
                if (node.Name.StartsWith("$FILE_NAME$"))
                {
                    _fileName = node.Name.Substring(11);
                    continue;
                }
                    
                switch (node.Name)
                {
                    case "Decorator":
                    case "Function":
                    case "AsyncFunction":
                    case "TypeClass":
                        scopePos++;
                        break;
                    case "Generic":
                    case "BindGenericInterface":
                        {
                            // perform generate checking
                            bool needsSubscope;
                            foreach (var generate in ((GenericType)(((BlockNode)node).Nodes[0].Type)).Generates)
                            {
                                // create new working scope for each generate
                                _table.AddScope();
                                _table.DescendScope();

                                foreach (var alias in generate.GenericAliases)
                                    _table.AddSymbol(new Symbol(alias.Key, new GenericAlias(alias.Value)));

                                needsSubscope = !generate.Block.Name.EndsWith("Function");

                                // create artificial scope for all who need it
                                if (needsSubscope)
                                    _table.AddScope();

                                int _ = 0;
                                _completeBlock(generate.Block, ref _);

                                // remove generate scope
                                _table.AscendScope();
                                _table.RemoveScope();
                            }                           
                        }

                        scopePos++;
                        break;
                    case "Interface":
                    case "BindInterface":
                        _table.GotoScope(scopePos);

                        _evaluateGenerates(((BlockNode)node).Block);

                        _table.AscendScope();

                        scopePos++;
                        break;
                    case "AnnotatedBlock":
                        {
                            BlockNode anode = (BlockNode)node;
                            StatementNode annotation = (StatementNode)anode.Block[0];

                            if (((ValueNode)annotation.Nodes[0]).Value == "friend")
                                _friendName = ((ValueNode)annotation.Nodes[1]).Value;

                            _evaluateGenerates(anode.Block);

                            if (_friendName != "")
                                _friendName = "";
                        }
                        break;
                }
            }
        }

        private void _completeFunction(BlockNode fn)
        {
            if (fn.Block.Count == 1)
            {
                _nodes.Add(fn);

                int scopeDepth = _table.GetScopeDepth();

                try
                {
                    var fnType = (FunctionType)fn.Nodes[0].Type;

                    for (int i = 0; i < fnType.Parameters.Count; i++)
                    {
                        var item = fnType.Parameters[i];

                        if (item.DefaultValue is IncompleteNode inode && _completeExprWithCtx(inode.AST, item.DataType, out ITypeNode cDefVal))
                            item.DefaultValue = cDefVal;      
                    }

                    _returnContext = fnType.ReturnType;

                    _visitFunctionBody(((IncompleteNode)fn.Block[0]).AST, fnType);

                    fn.Block = ((BlockNode)_nodes.Last()).Block;
                    fn.Block.RemoveAt(0); // remove incomplete node lol

                    // maybe add check to make sure all resources were accounted for
                }
                catch (SemanticException smex)
                {
                    smex.FileName = _fileName;
                    ErrorQueue.Add(smex);

                    fn.Block = new List<ITypeNode>();

                    if (scopeDepth != _table.GetScopeDepth())
                        _table.AscendScope();

                    _clearContext();
                }
                finally
                {                   
                    _returnContext = null;
                }

                
                _nodes.RemoveAt(1);
            }
        }

        private void _completeInterface(BlockNode interf, bool needsScope)
        {
            if (needsScope)
            {
                _table.AddScope();
                _table.DescendScope();
            }
            
            if (interf.Name == "Interface")
                _table.AddSymbol(new Symbol("$THIS", ((InterfaceType)interf.Nodes[0].Type).GetInstance()));
            else if (interf.Name == "BindGenerateInterface")
                _table.AddSymbol(new Symbol("$THIS", interf.Nodes[0].Type));
            // make sure generics are defined as instance         
            else
                // for all who need it, it is already declared as an instance
                _table.AddSymbol(new Symbol("$THIS", interf.Nodes[1].Type));

            foreach (var item in interf.Block)
            {
                // all of these types can use _completeFunction b/c they are basically the same
                if (item.Name.EndsWith("Function"))
                {
                    _isFinalizer = ((IdentifierNode)((BlockNode)item).Nodes[0]).IdName == "__finalize__";

                    _completeFunction((BlockNode)item);

                    _isFinalizer = false;
                }
                else if (item.Name == "OperatorOverload" || item.Name == "Variant")
                    _completeFunction((BlockNode)item);
            }

            _table.AscendScope();
        }

        // completes all sub generics (excluding generates) of a type class or interface
        private void _completeGenerics(BlockNode block, int scopePos)
        {
            _table.GotoScope(scopePos);

            int tPos = 0;
            foreach (var item in block.Block)
            {
                if (item.Name == "Generic")
                {
                    // visit normal generic body and generate necessary code
                    _table.GotoScope(tPos);

                    int _ = 0;
                    _completeBlock(((BlockNode)item).Block[0], ref _);

                    _table.AscendScope();

                    tPos++;
                }
                else if (item.Name.EndsWith("Function"))
                    tPos++;
            }

            _table.AscendScope();
        }

        // complete the expression content of an initializer with the necessary type context
        private bool _completeExprWithCtx(ASTNode node, DataType ctxType, out ITypeNode res)
        {
            int initNodeCount = _nodes.Count;

            try
            {
                _addContext(node);
                _visitExpr(node);
                _clearContext();

                if (_nodes.Last() is IncompleteNode inode)
                {
                    _giveContext(inode, ctxType);

                    _nodes[_nodes.Count - 2] = _nodes[_nodes.Count - 1];
                    _nodes.RemoveLast();
                }
                else if (!ctxType.Coerce(_nodes.Last().Type))
                    throw new SemanticException($"Initializer type of {_nodes.Last().Type.ToString()} not coercible to"
                        + $" desired type of {ctxType.ToString()}", node.Position);

                res = _nodes.Last();

                _nodes.RemoveLast();

                return true;
            }
            catch (SemanticException smex)
            {
                smex.FileName = _fileName;
                ErrorQueue.Add(smex);

                while (_nodes.Count != initNodeCount)
                    _nodes.RemoveLast();

                res = null;
                return false;
            }
        }
    }
}
