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
                case "enum_decl":
                    _visitEnum(root, modifiers);
                    break;
                case "template_decl":
                    _visitTemplate(root, modifiers);
                    break;
            }
        }

        private void _visitInterface(ASTNode node, List<Modifier> modifiers)
        {
            _nodes.Add(new BlockNode("Interface"));
            TokenNode name = (TokenNode)node.Content[1];

            // declare symbol subscope
            _table.AddScope();
            _table.DescendScope();

            var interfaceType = new InterfaceType();

            // flag setting next element to be private
            bool priv = false;
            foreach (var func in ((ASTNode)node.Content[3]).Content)
            {
                if (func.Name == "TOKEN")
                {
                    priv = true;
                    continue;
                }

                var memberModifiers = new List<Modifier>() { Modifier.CONSTANT };

                if (priv)
                {
                    memberModifiers.Add(Modifier.PRIVATE);
                    priv = false;
                }

                _visitFunction((ASTNode)func, memberModifiers);
                var fnNode = (IdentifierNode)((BlockNode)_nodes.Last()).Nodes[0];

                // add function to interface block
                MergeToBlock();

                // consider adding overloads to interfaces
                if (!interfaceType.AddFunction(new Symbol(fnNode.IdName, fnNode.Type, memberModifiers), ((ASTNode)func).Content.Last().Name == "func_body"))
                    throw new SemanticException("Interface cannot contain duplicate members", ((ASTNode)func).Content[1].Position);
            }

            _nodes.Add(new IdentifierNode(name.Tok.Value, interfaceType, true));
            MergeBack();

            _table.AscendScope();

            if (!_table.AddSymbol(new Symbol(name.Tok.Value, interfaceType, modifiers)))
                throw new SemanticException($"Unable to redeclare symbol by name `{name.Tok.Value}`", name.Position);
        }

        private void _visitStruct(ASTNode node, List<Modifier> modifiers)
        {
            _nodes.Add(new BlockNode("Struct"));
            TokenNode name = (TokenNode)node.Content[1];

            var structType = new StructType(name.Tok.Value, false);
           
            foreach (var subNode in ((ASTNode)node.Content[3]).Content)
            {
                if (subNode.Name == "struct_var")
                {
                    var processingStack = new List<TokenNode>();

                    foreach (var item in ((ASTNode)subNode).Content)
                    {
                        if (item.Name == "TOKEN" && ((TokenNode)item).Tok.Type == "IDENTIFIER")
                            processingStack.Add((TokenNode)item);
                        else if (item.Name == "types")
                        {
                            IDataType type = _generateType((ASTNode)item);

                            foreach (var member in processingStack)
                            {
                                if (!structType.AddMember(member.Tok.Value, type))
                                    throw new SemanticException("Structs cannot contain duplicate members", member.Position);
                            }
                        }
                    }
                }
            }

            _nodes.Add(new IdentifierNode(name.Tok.Value, structType, true));
            MergeBack();

            if (!_table.AddSymbol(new Symbol(name.Tok.Value, structType, modifiers)))
                throw new SemanticException($"Unable to redeclare symbol by name `{name.Tok.Value}`", name.Position);
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

                        if (decorType.MatchParameters(new List<IDataType>() { fnType }))
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

        private void _visitEnum(ASTNode node, List<Modifier> modifiers)
        {
            var values = ((ASTNode)node.Content[3]).Content.Select(x => ((TokenNode)x).Tok.Value).ToList();

            if (values.GroupBy(x => x).All(x => x.Count() > 1))
                throw new SemanticException("Enum cannot contain duplicate values", ((ASTNode)node.Content[3]).Content
                    .GroupBy(x => x.Name)
                    .Where(x => x.Count() > 1)
                    .First().First()
                    .Position
                    );

            _nodes.Add(new BlockNode("Enum"));

            string name = ((TokenNode)node.Content[1]).Tok.Value;
            EnumType et = new EnumType(name, values);

            _nodes.Add(new IdentifierNode(name, et, true));
            MergeBack();

            if (!_table.AddSymbol(new Symbol(name, et, modifiers)))
                throw new SemanticException($"Unable to redeclare symbol by name `{name}`", node.Content[1].Position);
        }
    }
}
