using System;
using System.Collections.Generic;
using System.Linq;

using Whirlwind.Parser;
using Whirlwind.Types;

namespace Whirlwind.Semantic.Visitor
{
    partial class Visitor
    {
        private void _visitTypeClass(ASTNode node)
        {
            _nodes.Add(new BlockNode("TypeClass"));

            string name = ((TokenNode)node.Content[1]).Tok.Value;
            ObjectType objType = new ObjectType(name, false, false);

            var interfaces = new List<InterfaceType>();

            foreach (var item in node.Content)
            {
                if (item.Name == "impl")
                    _visitImplClause((ASTNode)item, ref interfaces);
                else if (item.Name == "type_class_main")
                {
                    ASTNode decl = (ASTNode)((ASTNode)item).Content.Last();

                    List<Modifier> modifiers = new List<Modifier>();

                    if (((ASTNode)item).Content.Count > 1)
                        modifiers.Add(Modifier.PRIVATE);

                    // handle moving symbols from scope to type class
                    switch (decl.Name)
                    {
                        case "variable_decl":
                            _visitVarDecl(decl, modifiers);
                            break;
                    }
                }
            }
        }

        private void _visitImplClause(ASTNode node, ref List<InterfaceType> interfaces)
        {
            foreach (var elem in node.Content)
            {
                // all elements are tokens
                Token token = ((TokenNode)elem).Tok;

                if (token.Type == "IDENTIFIER")
                {
                    if (_table.Lookup(token.Value, out Symbol symbol))
                    {
                        if (symbol.DataType.Classify() == TypeClassifier.INTERFACE)
                        {
                            if (interfaces.Any(x => x.Equals(symbol.DataType)))
                                throw new SemanticException("Unable to inherit from an interface multiple times", elem.Position);

                            interfaces.Add((InterfaceType)symbol.DataType);
                        }
                        else
                            throw new SemanticException("Unable to implement a non-interface", elem.Position);
                    }
                    else
                        throw new SemanticException($"Undefined Symbol: `{token.Value}`", elem.Position);
                }
            }
        }
    }
}
