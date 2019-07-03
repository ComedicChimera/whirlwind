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

            _collectInterfaceMethods(interfaceType, (ASTNode)node.Content[node.Content[2].Name == "generic_tag" ? 4 : 3], false);

            _nodes.Add(new IdentifierNode(name, interfaceType));
            MergeBack();

            _table.AscendScope();

            if (!_table.AddSymbol(new Symbol(name, interfaceType, modifiers)))
                throw new SemanticException($"Unable to redeclare symbol by name `{name}`", node.Content[1].Position);
        }

        private void _visitInterfaceBind(ASTNode node)
        {
            _selfNeedsPointer = false;

            DataType dt;

            if (node.Content[1].Name == "generic_tag")
                _visitGenericBind(node);
            else
            {
                _nodes.Add(new BlockNode("BindInterface"));

                var interfaceType = new InterfaceType();

                _table.AddScope();
                _table.DescendScope();

                dt = _generateType((ASTNode)node.Content[2]);

                _collectInterfaceMethods(interfaceType, (ASTNode)node.Content[node.Content.Count - 2], true);

                interfaceType.Derive(dt);                    

                _nodes.Add(new ValueNode("Interface", interfaceType));
                _nodes.Add(new ValueNode("Type", dt));

                MergeBack(2);

                if (node.Content.Count > 5)
                {
                    _nodes.Add(new ExprNode("Implements", new VoidType()));

                    foreach (var item in ((ASTNode)node.Content[4]).Content)
                    {
                        if (item.Name == "types")
                        {
                            DataType impl = _generateType((ASTNode)item);

                            if (impl.Classify() == TypeClassifier.INTERFACE)
                            {
                                if (!((InterfaceType)impl).Derive(dt))
                                    throw new SemanticException("The given data type does not implement all required methods of the interface",
                                        item.Position);
                            }
                            else
                                throw new SemanticException("Type interface can only implement interfaces", item.Position);

                            _nodes.Add(new ValueNode("Implement", impl));
                            MergeBack();
                        }
                    }

                    MergeBack(); // merge to interface decl
                }

                _selfNeedsPointer = true;

                _table.AscendScope();
            }
        }

        private void _visitGenericBind(ASTNode node)
        {
            var genericVars = _primeGeneric((ASTNode)node.Content[1]);

            var dt = _generateType((ASTNode)node.Content[3]);

            if (dt is GenericType gt)
            {
                if (!gt.CompareGenerics(genericVars))
                    throw new SemanticException("Generic variables of generic binding must match those of generic type",
                        node.Content[1].Position);

                var gei = new GenericInterface((ASTNode)node.Content[node.Content.Count - 2]);

                _nodes.Add(new BlockNode("GenericInterfaceBind"));

                _nodes.Add(new ValueNode("Type", dt));
                MergeBack();

                var interfaceType = new InterfaceType();

                _collectInterfaceMethods(interfaceType, gei.Body, true);        

                if (node.Content[5].Name == "implements")
                {
                    foreach (var item in ((ASTNode)node.Content[5]).Content)
                    {
                        if (item.Name == "types")
                        {
                            var type = _generateType((ASTNode)item);

                            if (type.Classify() == TypeClassifier.INTERFACE)
                                gei.StandardImplements.Add((InterfaceType)type);
                            else if (type is GenericType gimpl && gimpl.DataType is InterfaceType)
                                gei.GenericImplements.Add(gimpl);
                            else
                                throw new SemanticException("Generic type interface can only implement interfaces and generic interfaces",
                                    item.Position);
                        }
                    }
                }

                gt.GenericInterface = gei;

                _selfNeedsPointer = true;

                _table.AscendScope();
            }
            else
                throw new SemanticException("Unable to create generic binding for non-generic type", node.Content[1].Position);
        }

        private void _collectInterfaceMethods(InterfaceType interfaceType, ASTNode block, bool typeInterface)
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

                    var args = decl.Content[3].Name == "args_decl_list" ? _generateArgsDecl((ASTNode)decl.Content[3]) : new List<Parameter>();
                    int argsOffset = args.Count > 0 ? 1 : 0;

                    DataType rtType = new VoidType();
                    if (decl.Content[4 + argsOffset].Name == "types")
                        rtType = _generateType((ASTNode)decl.Content[4 + argsOffset]);

                    var ft = new FunctionType(args, rtType, false);

                    _nodes.Add(new ValueNode("Operator", ft, op));
                    MergeBack();

                    if (decl.Content.Last().Name == "func_body")
                        _nodes.Add(new IncompleteNode((ASTNode)decl.Content.Last()));
                    else if (typeInterface)
                        throw new SemanticException("All methods of type interface must define a body",
                            decl.Content.Last().Position);
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

                    if (typeInterface && func.Content.Last().Name != "func_body")
                        throw new SemanticException("All methods of type interface must define a body",
                            func.Content[func.Content.Count - 2].Position);

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
