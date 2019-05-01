using System.Linq;
using System.Collections.Generic;

using Whirlwind.Parser;
using Whirlwind.Types;

namespace Whirlwind.Semantic.Visitor
{
    partial class Visitor
    {
        private void _visitInterface(ASTNode node, List<Modifier> modifiers)
        {
            // declare symbol subscope
            _table.AddScope();
            _table.DescendScope();

            var interfaceType = new InterfaceType();

            _nodes.Add(new BlockNode("Interface"));

            string name = ((TokenNode)node.Content[1]).Tok.Value;

            // declare self referential type (ok early b/c reference)
            _table.AddSymbol(new Symbol(name, new SelfType(interfaceType) { Constant = true }));

            _collectInterfaceMethods(interfaceType, (ASTNode)node.Content[node.Content[2].Name == "generic_tag" ? 4 : 3]);

            _nodes.Add(new IdentifierNode(name, interfaceType));
            MergeBack();

            _table.AscendScope();

            if (!_table.AddSymbol(new Symbol(name, interfaceType, modifiers)))
                throw new SemanticException($"Unable to redeclare symbol by name `{name}`", node.Content[1].Position);
        }

        private void _visitInterfaceBind(ASTNode node)
        {
            _selfNeedsPointer = false;

            _table.AddScope();
            _table.DescendScope();

            var interfaceType = new InterfaceType();
            int genericOffset = 0;

            // calculate generic offset account for generic interface bind
            // which is now "interf<T> for A", NOT "interf<T> for A<T>"


            DataType dt = _generateType((ASTNode)node.Content[2]);

            // implement interface
            if (!interfaceType.Derive(dt))
                throw new SemanticException("All methods of type interface must define a body", node.Content[2].Position);

            _nodes.Add(new ExprNode("BindInterface", interfaceType));

            _nodes.Add(new ValueNode("Interface", interfaceType));
            _nodes.Add(new ValueNode("Type", dt));

            MergeBack(2);
            MergeBack(); // merge to interface decl

            if (node.Content.Count > 3)
            {
                _nodes.Add(new ExprNode("Implements", new SimpleType()));

                foreach (var item in node.Content.Skip(4))
                {
                    if (item.Name == "types")
                    {
                        DataType impl = _generateType((ASTNode)item);

                        if (impl.Classify() != TypeClassifier.INTERFACE)
                            throw new SemanticException("Type interface can only implement interfaces", item.Position);

                        if (!((InterfaceType)impl).Derive(dt))
                            throw new SemanticException("The given data type does not implement all required methods of the interface",
                                item.Position);

                        _nodes.Add(new ValueNode("Implement", impl));
                        MergeBack();
                    }
                }

                MergeBack(); // merge to interface decl
            }

            _selfNeedsPointer = true;

            _table.AscendScope();
        }

        private void _collectInterfaceMethods(InterfaceType interfaceType, ASTNode block)
        {
            foreach (var method in block.Content)
            {
                var memberModifiers = new List<Modifier>();

                if (method.Name == "variant_decl")
                    _visitVariant((ASTNode)method);
                else if (method.Name == "operator_decl")
                {
                    _nodes.Add(new BlockNode("OperatorOverload"));

                    ASTNode decl = (ASTNode)method;

                    string op = ((ASTNode)decl.Content[1]).Content
                        .Select(x => ((TokenNode)x).Tok.Type)
                        .Aggregate((a, b) => a + b);

                    var args = _generateArgsDecl((ASTNode)decl.Content[3]);

                    DataType rtType = new SimpleType();
                    if (decl.Content[5].Name == "types")
                        rtType = _generateType((ASTNode)decl.Content[5]);

                    var ft = new FunctionType(args, rtType, false);

                    _nodes.Add(new ValueNode("Operator", ft, op));
                    MergeBack();

                    if (args.Count == 0 && decl.Content[5].Name == "func_body")
                        _nodes.Add(new IncompleteNode((ASTNode)decl.Content[5]));
                    else if (args.Count > 0 && decl.Content[6].Name == "func_body")
                        _nodes.Add(new IncompleteNode((ASTNode)decl.Content[6]));
                    else
                    {
                        interfaceType.AddMethod(new Symbol($"__{op}__", ft), false);

                        // merge operator overload
                        MergeToBlock();
                        continue;
                    }

                    interfaceType.AddMethod(new Symbol($"__{op}__", ft), true);

                    MergeToBlock();
                }
                else
                {
                    ASTNode func = (ASTNode)method;

                    if (func.Content[2].Name == "generic_tag")
                    {
                        var genericVars = _primeGeneric((ASTNode)func.Content[2]);

                        _visitFunction(func, memberModifiers);

                        _makeGeneric(func, genericVars, memberModifiers, func.Content[1].Position);

                        var genNode = (IdentifierNode)((BlockNode)_nodes.Last()).Nodes[0];

                        if (!interfaceType.AddMethod(new Symbol(genNode.IdName, genNode.Type, memberModifiers),
                            func.Content.Last().Name == "func_body"))
                            throw new SemanticException("Interface cannot contain duplicate members", func.Content[1].Position);
                    }
                    else
                    {
                        _visitFunction(func, memberModifiers);
                        var fnNode = (IdentifierNode)((BlockNode)_nodes.Last()).Nodes[0];

                        if (!interfaceType.AddMethod(new Symbol(fnNode.IdName, fnNode.Type, memberModifiers),
                            func.Content.Last().Name == "func_body"))
                            throw new SemanticException("Interface cannot contain duplicate members", func.Content[1].Position);
                    }
                }

                // add function to interface block
                MergeToBlock();
            }
        }
    }
}
