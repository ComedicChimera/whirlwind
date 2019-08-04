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

            _evaluateGenerates(block);
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

                    scopePos++;
                    break;
                case "Interface":
                case "BindInterface":
                case "BindGenericInterface":
                case "BindGenerateInterface":
                    _completeInterface((BlockNode)node);
                    _table.MoveScope(_table.GetScopeCount() - 1, scopePos);

                    // complete all sub generics (avoid scope complications)
                    _completeGenerics((BlockNode)node, scopePos);

                    scopePos++;
                    break;
                case "Generic":
                    {
                        _table.GotoScope(scopePos);

                        /*foreach (var name in ((GenericType)((TreeNode)node).Nodes[0].Type).GenericNames)
                            _table.AddSymbol(new Symbol(name, new GenericPlaceholder(name)));*/

                        int _ = 0;
                        _completeBlock(((BlockNode)node).Block[0], ref _);

                        _table.AscendScope();
                    }

                    scopePos++;
                    break;
                case "Variant":
                    // variants are basically functions for this stage of compilation
                    _completeFunction((BlockNode)node);
                    break;
                // add completion for structs
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
                        }

                        _table.AscendScope();
                    }

                    scopePos++;
                    break;
            }
        }

        private void _evaluateGenerates(List<ITypeNode> block)
        {
            int scopePos = 0;

            foreach (var node in block)
            {
                switch (node.Name)
                {
                    case "Decorator":
                    case "Function":
                    case "AsyncFunction":
                        scopePos++;
                        break;
                    case "Generic":
                    case "BindGenericInterface":
                        {
                            // perform generate checking (clean up scope and nodes after done)
                            // create working scope for clean generic generate checking
                            _table.AddScope();
                            _table.DescendScope();

                            int tPos = 0;

                            foreach (var generate in ((GenericType)(((BlockNode)node).Nodes[0].Type)).Generates)
                            {
                                foreach (var alias in generate.GenericAliases)
                                    _table.AddSymbol(new Symbol(alias.Key, new GenericAlias(alias.Value)));

                                // create artificial scope for all who need it
                                if (!generate.Block.Name.EndsWith("Function"))
                                    _table.AddScope();

                                int refTemp = tPos;
                                _completeBlock(generate.Block, ref refTemp);

                                tPos++;
                            }

                            // clean up scope
                            _table.AscendScope();
                            _table.RemoveScope();
                        }

                        scopePos++;
                        break;
                    case "TypeClass":
                    case "Interface":
                    case "BindInterface":
                        _table.GotoScope(scopePos);

                        _evaluateGenerates(((BlockNode)node).Block);

                        _table.AscendScope();

                        scopePos++;
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
                    _visitFunctionBody(((IncompleteNode)fn.Block[0]).AST, (FunctionType)fn.Nodes[0].Type);

                    fn.Block = ((BlockNode)_nodes.Last()).Block;
                    fn.Block.RemoveAt(0); // remove incomplete node lol
                }
                catch (SemanticException se)
                {
                    ErrorQueue.Add(se);

                    fn.Block = new List<ITypeNode>();

                    if (scopeDepth != _table.GetScopeDepth())
                        _table.AscendScope();

                    _clearPossibleContext();
                }
                
                _nodes.RemoveAt(1);
            }
        }

        private void _completeInterface(BlockNode interf)
        {
            _table.AddScope();
            _table.DescendScope();

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
                // all of these types are effectively the same
                if (item.Name.EndsWith("Function") || item.Name == "OperatorOverload" || item.Name == "Variant")
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
    }
}
