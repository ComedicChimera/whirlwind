using Whirlwind.Parser;
using Whirlwind.Types;

using System.Collections.Generic;
using System.Linq;

namespace Whirlwind.Semantic.Visitor
{
    partial class Visitor
    {
        private void _visitBlockDecl(ASTNode node, List<Modifier> modifiers)
        {
            ASTNode root = (ASTNode)node.Content[0];

            switch (root.Name)
            {
                case "type_class_decl":
                    _visitTypeClass(root, modifiers);
                    break;
                case "func_decl":
                    _visitFunction(root, modifiers);
                    break;
                case "interface_decl":
                    _visitInterface(root, modifiers);
                    break;
                case "struct_decl":
                    _visitStruct(root, modifiers);
                    break;
                case "decor_decl":
                    _visitDecorator(root, modifiers);
                    break;
                case "template_decl":
                    _visitTemplate(root, modifiers);
                    break;
                case "variant_decl":
                    _visitVariant(root);
                    break;
            }
        }

        private void _visitInterface(ASTNode node, List<Modifier> modifiers)
        {
            // declare symbol subscope
            _table.AddScope();
            _table.DescendScope();

            var interfaceType = new InterfaceType();

            TokenNode tn = (TokenNode)node.Content[1];

            if (tn.Tok.Type == "FOR")
                _nodes.Add(new BlockNode("TypeInterface"));
            else
            {
                _nodes.Add(new BlockNode("Interface"));

                // declare self referential type (ok early b/c reference)
                _table.AddSymbol(new Symbol(tn.Tok.Value, new SelfType(interfaceType), new List<Modifier> { Modifier.CONSTANT }));
            }
                

            foreach (var func in ((ASTNode)node.Content[3]).Content)
            {
                var memberModifiers = new List<Modifier>() { Modifier.CONSTANT };

                if (func.Name == "method_template")
                {
                    _visitTemplate((ASTNode)func, memberModifiers);
                    var fnNode = (IdentifierNode)((BlockNode)_nodes.Last()).Nodes[0];

                    if (!interfaceType.AddTemplate(new Symbol(fnNode.IdName, fnNode.Type, memberModifiers),
                        ((ASTNode)((ASTNode)func).Content.Last()).Content.Last().Name == "func_body"))
                        throw new SemanticException("Interface cannot contain duplicate members", ((ASTNode)func).Content[1].Position);
                }
                else if (func.Name == "variant_decl")
                    _visitVariant((ASTNode)func);
                else if (func.Name == "operator_decl")
                {
                    _nodes.Add(new BlockNode("OperatorOverload"));

                    ASTNode decl = (ASTNode)func;

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
                    _visitFunction((ASTNode)func, memberModifiers);
                    var fnNode = (IdentifierNode)((BlockNode)_nodes.Last()).Nodes[0];

                    if (!interfaceType.AddMethod(new Symbol(fnNode.IdName, fnNode.Type, memberModifiers),
                        ((ASTNode)func).Content.Last().Name == "func_body"))
                        throw new SemanticException("Interface cannot contain duplicate members", ((ASTNode)func).Content[1].Position);
                }
                
                // add function to interface block
                MergeToBlock();                
            }

            if (tn.Tok.Value == "FOR")
            {
                _selfNeedsPointer = false;

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

            }
            else
            {
                string name = tn.Tok.Value;

                _nodes.Add(new IdentifierNode(name, interfaceType, true));
                MergeBack();

                _table.AscendScope();

                if (!_table.AddSymbol(new Symbol(name, interfaceType, modifiers)))
                    throw new SemanticException($"Unable to redeclare symbol by name `{name}`", tn.Position);
            }
        }

        private void _visitDecorator(ASTNode node, List<Modifier> modifiers)
        {
            _visitFunction((ASTNode)node.Content[1], modifiers);

            FunctionType fnType = (FunctionType)((TreeNode)_nodes.Last()).Nodes[0].Type;

            _nodes.Add(new BlockNode("Decorator"));
         
            foreach (var item in ((ASTNode)node.Content[0]).Content)
            {
                if (item.Name == "expr")
                {
                    _visitExpr((ASTNode)item);

                    if (_nodes.Last().Type.Classify() == TypeClassifier.FUNCTION)
                    {
                        FunctionType decorType = (FunctionType)_nodes.Last().Type;

                        if (decorType.MatchArguments(new ArgumentList(new List<DataType>() { fnType })))
                        {
                            // check for void decorators
                            if (_isVoid(decorType.ReturnType))
                                throw new SemanticException("A decorator must return a value", item.Position);

                            // allows decorator to override function return type ;)
                            if (!fnType.Coerce(decorType.ReturnType))
                            {
                                _table.Lookup(((TokenNode)((ASTNode)node.Content[1]).Content[1]).Tok.Value, out Symbol sym);

                                _table.ReplaceSymbol(sym.Name, new Symbol(sym.Name, decorType.ReturnType, sym.Modifiers));
                            }

                            MergeBack();
                        }
                        else
                            throw new SemanticException("This decorator is not valid for the given function", item.Position);
                    }
                    else
                        throw new SemanticException("Unable to use non-function as a decorator", item.Position);
                }
            }

            PushToBlock();
        }
    }
}
