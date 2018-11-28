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

            if (!_table.AddSymbol(new Symbol(name.Tok.Value, structType, 
                _exported ? new List<Modifier>() { Modifier.EXPORTED, Modifier.CONSTANT } 
                : new List<Modifier>() { Modifier.CONSTANT })))
                throw new SemanticException($"Unable to redeclare symbol by name `{name.Tok.Value}`", name.Position);
        }
    }
}
