using System.Linq;
using System.Collections.Generic;

using Whirlwind.Syntax;
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
            if (_isGenericSelfContext)
            {
                // if there's context, the symbol exists
                _table.Lookup("$GENERIC_SELF", out Symbol genSelf);
                _table.AddSymbol(new Symbol(name, genSelf.DataType));
            }
            else
                _table.AddSymbol(new Symbol(name, new SelfType(_namePrefix + name, interfaceType) { Constant = true }));

            _collectInterfaceMethods(interfaceType, (ASTNode)node.Content[node.Content[2].Name == "generic_tag" ? 4 : 3], false);

            _nodes.Add(new IdentifierNode(name, interfaceType));
            MergeBack();

            // update self type if necessary
            if (_table.Lookup(name, out Symbol selfSym) && selfSym.DataType is SelfType)
                ((SelfType)selfSym.DataType).Initialized = true;
            
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

                _nodes.Add(new ValueNode("TypeInterface", interfaceType));
                _nodes.Add(new ValueNode("BindType", dt));

                MergeBack(2);

                if (node.Content.Count > 5)
                {
                    _nodes.Add(new ExprNode("Implements", new VoidType()));

                    foreach (var item in ((ASTNode)node.Content[4]).Content)
                    {
                        if (item.Name == "types")
                        {
                            DataType impl = _generateType((ASTNode)item);

                            if (impl is InterfaceType it && !it.SuperForm)
                            {
                                if (!it.Derive(dt))
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

            _nodes.Add(new BlockNode("BindGenericInterface"));

            var ifType = new InterfaceType();
            _collectInterfaceMethods(ifType, (ASTNode)node.Content[node.Content.Count - 2], true);

            var generic = new GenericType(genericVars, ifType, _decorateEval(node, 
                delegate (ASTNode ifBind, List<Modifier> modifiers)
                {
                    var newType = new InterfaceType();

                    _nodes.Add(new BlockNode("BindGenerateInterface"));
                    _collectInterfaceMethods(newType, (ASTNode)ifBind.Content[ifBind.Content.Count - 2], true);

                    _nodes.Add(new ValueNode("GenerateThis", _generateType((ASTNode)ifBind.Content[2])));
                    MergeBack();

                    // no need to add implements because processed later

                    _table.AddSymbol(new Symbol("$GENERATE_BIND", newType));
                }
                ));

            var bindTypeNode = (ASTNode)node.Content[3];
            var bindDt = _generateType(bindTypeNode);

            _nodes.Add(new ValueNode("GenericTypeInterface", generic));
            _nodes.Add(new ValueNode("BindType", bindDt));
            MergeBack(2);

            var genericBinding = new GenericBinding(generic);

            if (node.Content[node.Content.Count - 4] is ASTNode implNode && implNode.Name == "implements")
            {
                _nodes.Add(new ExprNode("Implements", new VoidType()));

                foreach (var item in implNode.Content)
                {
                    if (item.Name == "types")
                    {
                        var typeNode = (ASTNode)item;
                        var dt = _generateType((ASTNode)item);

                        if (_isGenericInterf(typeNode, genericVars.Select(x => x.Name).ToList()))
                        {
                            genericBinding.GenericImplements.Add(new GenericType(genericVars, dt, _decorateEval(typeNode, _typeVFN)));
                            _nodes.Add(new ValueNode("GenericInherit", dt));
                        }                           
                        else
                        {
                            if (dt is InterfaceType it)
                                genericBinding.StandardImplements.Add(it);
                            else
                                throw new SemanticException("Unable to use non-interface as a classifying interface", item.Position);

                            _nodes.Add(new ValueNode("StandardInherit", dt));                           
                        }

                        MergeBack();
                    }  
                }

                MergeBack();
            }

            InterfaceRegistry.GenericInterfaces.Add(
                new GenericBindDiscriminator(genericVars, new GenericType(genericVars, bindDt, 
                _decorateEval(bindTypeNode, _typeVFN))),
                genericBinding
                );

            _selfNeedsPointer = true;
            _table.AscendScope();
        }

        private void _collectInterfaceMethods(InterfaceType interfaceType, ASTNode block, bool typeInterface)
        {
            _functionCanHaveNoBody = true;

            foreach (var method in block.Content)
            {
                var memberModifiers = new List<Modifier>();

                if (method.Name == "variant_decl")
                    _visitVariant((ASTNode)method);
                else if (method.Name == "operator_decl")
                    _visitOperatorOverload(interfaceType, (ASTNode)method, typeInterface);
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

                        _makeGeneric(func, genericVars, memberModifiers, _table.GetScope().Last(), func.Content[1].Position);

                        var genNode = (IdentifierNode)((BlockNode)_nodes.Last()).Nodes[0];

                        if (genNode.IdName == "__finalize__")
                            throw new SemanticException("Finalizers cannot be generic", func.Content[2].Position);

                        if (!interfaceType.AddMethod(new Symbol(genNode.IdName, genNode.Type, memberModifiers),
                            func.Content.Last().Name == "func_body"))
                            throw new SemanticException("Interface cannot contain duplicate members", func.Content[1].Position);
                    }
                    else
                    {
                        _visitFunction(func, memberModifiers);
                        var fnNode = (IdentifierNode)((BlockNode)_nodes.Last()).Nodes[0];

                        if (fnNode.IdName == "__finalize__")
                        {
                            var fn = ((FunctionType)fnNode.Type);

                            if (fn.Async || fn.Parameters.Count > 0 || fn.ReturnType.Classify() != TypeClassifier.VOID)
                                throw new SemanticException("Invalid definition for finalizer", func.Content[1].Position);
                        }

                        if (!interfaceType.AddMethod(new Symbol(fnNode.IdName, fnNode.Type, memberModifiers),
                            func.Content.Last().Name == "func_body"))
                            throw new SemanticException("Interface cannot contain duplicate members", func.Content[1].Position);
                    }
                }

                // add function to interface block
                MergeToBlock();

                _functionCanHaveNoBody = false;
            }
        }

        private void _visitOperatorOverload(InterfaceType interfType, ASTNode node, bool typeInterface)
        {
            _nodes.Add(new BlockNode("OperatorOverload"));

            string op = "";
            var args = new List<Parameter>();
            DataType rtType = new VoidType();
            var genericVars = new List<GenericVariable>();

            foreach (var subNode in node.Content)
            {
                switch (subNode.Name)
                {
                    case "operator":
                    case "ext_op":
                        op = ((ASTNode)subNode).Content
                        .Select(x => ((TokenNode)x).Tok.Type)
                        .Aggregate((a, b) => a + b);
                        break;
                    case "generic_tag":
                        genericVars = _primeGeneric((ASTNode)subNode);
                        break;
                    case "args_decl_list":
                        args = _generateArgsDecl((ASTNode)subNode);
                        break;
                    case "types":
                        rtType = _generateType((ASTNode)subNode);
                        break;
                }
            }

            if (new[] { "!", ":>", "<-", "~" }.Contains(op))
            {
                if (args.Count != 0)
                    throw new SemanticException("Unary operator overload must not take arguments", node.Content[3].Position);
            }               
            else if (op == "[:]")
            {
                if (args.Count != 3 && args.Count != 4)
                    throw new SemanticException("Slice operator overload must take either 3 or 4 arguments", node.Content[3].Position);
            }               
            else if (op == "[]")
            {
                if (args.Count != 1 && args.Count != 2)
                    throw new SemanticException("Subscript operator overload must take either 1 or 2 arguments", node.Content[3].Position);
            }               
            else if ((op != "-" || args.Count != 0) && args.Count != 1)
                throw new SemanticException("Binary operator overload must take exactly 1 argument", node.Content[3].Position);

            var ft = new FunctionType(args, rtType, false);

            _nodes.Add(new ValueNode("Operator", ft, op));
            MergeBack();

            bool hasBody = false;

            if (node.Content.Last().Name == "func_body")
            {
                _nodes.Add(new IncompleteNode((ASTNode)node.Content.Last()));
                hasBody = true;
                MergeToBlock();
            }           
            else if (typeInterface)
                throw new SemanticException("All methods of type interface must define a body",
                    node.Content.Last().Position);

            var sym = new Symbol($"__{op}__", ft);

            if (genericVars.Count > 0)
            {
                _makeGeneric(node, genericVars, new List<Modifier>(), sym, node.Content[1].Position);
                var genType = ((BlockNode)_nodes.Last()).Nodes[0].Type;

                interfType.AddMethod(new Symbol($"__{op}__", genType), hasBody);
            }
            else
                interfType.AddMethod(new Symbol($"__{op}__", ft), hasBody);
        }

        private bool _isGenericInterf(ASTNode node, List<string> varNames)
        {
            foreach (var item in node.Content)
            {
                if (item is TokenNode tk && tk.Tok.Type == "IDENTIFIER" && varNames.Contains(tk.Tok.Value))
                    return true;
                else if (item is ASTNode anode && _isGenericInterf(anode, varNames))
                    return true;
            }

            return false;
        }

        private void _typeVFN(ASTNode typeNode, List<Modifier> modifiers)
        {
            var dt = _generateType(typeNode);
            
            // _decorateEval requires vfn to "produce" a block node
            _nodes.Add(new BlockNode("TypeGenerateBlock"));
            _nodes.Add(new ValueNode("Type", dt));
            MergeBack();

            _table.AddSymbol(new Symbol("$TYPE", dt));
        }
    }
}
