﻿using System.Linq;
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

            _nodes.Add(new BlockNode("Interface"));

            string name = ((TokenNode)node.Content[1]).Tok.Value;

            var interfaceType = new InterfaceType(_namePrefix + name);

            DataType selfType;

            // declare self referential type (ok early b/c reference)
            if (_isGenericSelfContext)
            {
                // if there's context, the symbol exists
                _table.Lookup("$GENERIC_SELF", out Symbol genSelf);

                selfType = genSelf.DataType;
                _table.AddSymbol(new Symbol(name, selfType));

                var genSelfType = (GenericSelfType)selfType;

                genSelfType.GetInstance(genSelfType.GenericVariables.Select(x => (DataType)new GenericPlaceholder(x.Name)).ToList(), 
                    out DataType gsit);

                _collectInterfaceMethods(interfaceType, (ASTNode)node.Content[node.Content[2].Name == "generic_tag" ? 4 : 3], 
                    false, gsit);
            }
            else
            {
                selfType = new SelfType(_namePrefix + name, interfaceType) { Constant = true };
                _table.AddSymbol(new Symbol(name, selfType));

                _collectInterfaceMethods(interfaceType, (ASTNode)node.Content[3], false, selfType);
            }          

            _nodes.Add(new IdentifierNode(name, interfaceType));
            MergeBack();

            // update self type if necessary
            if (selfType is SelfType st)
                st.Initialized = true;
            
            _table.AscendScope();

            if (!_table.AddSymbol(new Symbol(name, interfaceType, modifiers)))
                throw new SemanticException($"Unable to redeclare symbol: `{name}`", node.Content[1].Position);
        }

        private void _visitInterfaceBind(ASTNode node)
        {
            _selfNeedsPointer = false;
            _enableIntrinsicGet = true;

            DataType dt;

            if (node.Content[1].Name == "generic_tag")
                _visitGenericBind(node);
            else
            {
                _nodes.Add(new BlockNode("BindInterface"));

                _table.AddScope();
                _table.DescendScope();

                dt = _generateType((ASTNode)node.Content[2]);

                var interfaceType = new InterfaceType("TypeInterf:" + dt.ToString());

                _collectInterfaceMethods(interfaceType, (ASTNode)node.Content[node.Content.Count - 2], true, dt);

                interfaceType.Derive(dt);                    

                _nodes.Add(new ValueNode("TypeInterface", interfaceType));
                _nodes.Add(new ValueNode("BindType", dt));

                MergeBack(2);

                if (node.Content.Count > 5)
                {
                    _nodes.Add(new ExprNode("Implements", new NoneType()));

                    foreach (var item in ((ASTNode)node.Content[4]).Content)
                    {
                        if (item.Name == "types")
                        {
                            DataType impl = _generateType((ASTNode)item);

                            if (impl is InterfaceType it && !it.SuperForm)
                            {
                                if (!it.Derive(dt))
                                    throw new SemanticException($"Type of {dt.ToString()} does not implement all required methods of the interface",
                                        item.Position);
                            }
                            else
                                throw new SemanticException("Type interface can only implement interfaces", item.Position);

                            _nodes.Add(new ValueNode("StandardImplement", impl));
                            MergeBack();
                        }
                    }

                    MergeBack(); // merge to interface decl
                }

                _selfNeedsPointer = true;

                _table.AscendScope();
            }

            _enableIntrinsicGet = false;
        }

        private void _visitGenericBind(ASTNode node)
        {
            var genericVars = _primeGeneric((ASTNode)node.Content[1]);

            _nodes.Add(new BlockNode("BindGenericInterface"));

            var bindTypeNode = (ASTNode)node.Content[3];
            var bindDt = _generateType(bindTypeNode);

            var ifType = new InterfaceType("GenericTypeInterf" + bindDt.ToString());
            _collectInterfaceMethods(ifType, (ASTNode)node.Content[node.Content.Count - 2], true, bindDt);

            var generic = new GenericType(genericVars, ifType, _decorateEval(node,
                delegate (ASTNode ifBind, List<Modifier> modifiers)
                {
                    var newDt = _generateType((ASTNode)ifBind.Content[2]);

                    var newType = new InterfaceType("TypeInterf:" + newDt.ToString());

                    _nodes.Add(new BlockNode("BindGenerateInterface"));
                    _collectInterfaceMethods(newType, (ASTNode)ifBind.Content[ifBind.Content.Count - 2], true, newDt);

                    // no need to add implements because processed later
                    _nodes.Add(new ValueNode("GenerateThis", newDt));
                    MergeBack();

                    _table.AddSymbol(new Symbol("$GENERATE_BIND", newType));
                }
                ));

            _nodes.Add(new ValueNode("GenericTypeInterface", generic));
            _nodes.Add(new ValueNode("BindType", bindDt));
            MergeBack(2);

            var genericBinding = new GenericBinding(generic);

            if (node.Content[node.Content.Count - 4] is ASTNode implNode && implNode.Name == "implements")
            {
                _nodes.Add(new ExprNode("Implements", new NoneType()));

                foreach (var item in implNode.Content)
                {
                    if (item.Name == "types")
                    {
                        var typeNode = (ASTNode)item;
                        var dt = _generateType((ASTNode)item);

                        if (_isGenericInterf(typeNode, genericVars.Select(x => x.Name).ToList()))
                        {
                            genericBinding.GenericImplements.Add(new GenericType(genericVars, dt, _decorateEval(typeNode, _typeVFN)));
                            _nodes.Add(new ValueNode("GenericImplement", dt));
                        }                           
                        else
                        {
                            if (dt is InterfaceType it)
                                genericBinding.StandardImplements.Add(it);
                            else
                                throw new SemanticException("Unable to use non-interface as a classifying interface", item.Position);

                            _nodes.Add(new ValueNode("StandardImplement", dt));                           
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

        private void _collectInterfaceMethods(InterfaceType interfaceType, ASTNode block, bool typeInterface, DataType selfType)
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

                        if (new[] { "__finalize__", "__copy__", "__get__", "__set__" }.Contains(genNode.IdName))
                            throw new SemanticException("Special methods cannot be generic", func.Content[2].Position);

                        if (!interfaceType.AddMethod(new Symbol(genNode.IdName, genNode.Type, memberModifiers),
                            func.Content.Last().Name == "func_body"))
                            throw new SemanticException("Interface cannot contain duplicate members", func.Content[1].Position);
                    }
                    else
                    {
                        _visitFunction(func, memberModifiers);
                        var fnNode = (IdentifierNode)((BlockNode)_nodes.Last()).Nodes[0];

                        if (fnNode.IdName.StartsWith("__"))
                            _checkSpecialMethod(fnNode, selfType, func.Content[1].Position);


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
            DataType rtType = new NoneType();
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

        private void _checkSpecialMethod(IdentifierNode fnNode, DataType selfType, TextPosition namePos)
        {
            var fn = ((FunctionType)fnNode.Type);

            if (fnNode.IdName == "__finalize__")
            {
                if (fn.Async || fn.Parameters.Count > 0 || fn.ReturnType.Classify() != TypeClassifier.NONE)
                    throw new SemanticException("Invalid definition for finalizer", namePos);
            }
            else if (fnNode.IdName == "__copy__")
            {
                if (fn.Async || fn.Parameters.Count != 0 || !selfType.Equals(fn.ReturnType))
                    throw new SemanticException("Invalid definition for copier", namePos);
            }
            else if (fnNode.IdName == "__get__")
            {
                if (fn.Async || fn.Parameters.Count != 0 || !selfType.Equals(fn.ReturnType))
                    throw new SemanticException("Invalid definition for getter", namePos);
            }
            else if (fnNode.IdName == "__set__")
            {
                if (fn.Async || fn.Parameters.Count != 1 || !selfType.Equals(fn.Parameters.First().DataType) 
                    || fn.ReturnType.Classify() != TypeClassifier.NONE)
                    throw new SemanticException("Invalid definition for copier", namePos);
            }
        }
    }
}
