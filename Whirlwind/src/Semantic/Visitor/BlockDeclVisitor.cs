using Whirlwind.Parser;
using Whirlwind.Types;

using System.Collections.Generic;
using System.Linq;

namespace Whirlwind.Semantic.Visitor
{
    partial class Visitor
    {
        private bool _exported = false;

        private void _visitBlockDecl(ASTNode node)
        {
            ASTNode root = (ASTNode)node.Content[0];

            switch (root.Name)
            {
                case "func_decl":
                    _visitFunction(root);
                    break;
                case "interface_decl":
                    _visitInterface(root);
                    break;
                case "struct_decl":
                    _visitStruct(root);
                    break;
                case "decor_decl":
                    _visitDecorator(root);
                    break;
                case "enum_decl":
                    _visitEnum(root);
                    break;
            }
        }

        private void _visitInterface(ASTNode node)
        {
            _nodes.Add(new BlockNode("Interface"));
            TokenNode name = (TokenNode)node.Content[1];

            var interfaceType = new InterfaceType();

            foreach (var func in ((ASTNode)node.Content[3]).Content)
            {
                _visitFunction((ASTNode)func);
                var fnNode = (IdentifierNode)((BlockNode)_nodes.Last()).Nodes[0];

                // add function to interface block
                MergeToBlock();

                if (((ASTNode)func).Content.Last().Name == "func_body")
                {
                    if (!interfaceType.AddFunction(new Symbol(fnNode.IdName, fnNode.Type), (ASTNode)((ASTNode)func).Content.Last()))
                        throw new SemanticException("Interface cannot contain duplicate members", ((ASTNode)func).Content[1].Position);
                }
                else
                {
                    if (!interfaceType.AddFunction(new Symbol(fnNode.IdName, fnNode.Type)))
                        throw new SemanticException("Interface cannot contain duplicate members", ((ASTNode)func).Content[1].Position);
                }
            }

            _nodes.Add(new IdentifierNode(name.Tok.Value, interfaceType, true));
            MergeBack();

            if (!_table.AddSymbol(new Symbol(name.Tok.Value, interfaceType)))
                throw new SemanticException($"Unable to redeclare symbol by name `{name.Tok.Value}`", name.Position);
        }

        private void _visitStruct(ASTNode node)
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

            if (!_table.AddSymbol(new Symbol(name.Tok.Value, structType, 
                _exported ? new List<Modifier>() { Modifier.EXPORTED, Modifier.CONSTANT } 
                : new List<Modifier>() { Modifier.CONSTANT })))
                throw new SemanticException($"Unable to redeclare symbol by name `{name.Tok.Value}`", name.Position);
        }

        private void _visitDecorator(ASTNode node)
        {
            _visitFunction((ASTNode)node.Content[1]);

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

        private void _visitEnum(ASTNode node)
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

            if (!_table.AddSymbol(new Symbol(name, et, new List<Modifier>() { Modifier.CONSTANT })))
                throw new SemanticException($"Unable to redeclare symbol by name `{name}`", node.Content[1].Position);
        }
    }
}
