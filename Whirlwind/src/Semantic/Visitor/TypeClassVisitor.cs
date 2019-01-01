using System.Collections.Generic;
using System.Linq;

using Whirlwind.Parser;
using Whirlwind.Types;

namespace Whirlwind.Semantic.Visitor
{
    partial class Visitor
    {
        private void _visitTypeClass(ASTNode node, List<Modifier> typeModifiers)
        {
            _nodes.Add(new BlockNode("TypeClass"));

            string name = ((TokenNode)node.Content[1]).Tok.Value;
            ObjectType objType = new ObjectType(name, false, false);

            var interfaces = new List<InterfaceType>();

            _table.AddScope();
            _table.DescendScope();

            bool needsDefaultConstr = true;

            foreach (var item in node.Content)
            {
                if (item.Name == "impl")
                    _visitImplClause((ASTNode)item, ref interfaces);
                else if (item.Name == "type_class_main")
                {
                    ASTNode decl = (ASTNode)((ASTNode)item).Content.Where(x => x.Name != "TOKEN").Last();

                    List<Modifier> modifiers = new List<Modifier>() { Modifier.CONSTANT };

                    if (((ASTNode)item).Content.Count > 1)
                        modifiers.Add(Modifier.PRIVATE);

                    // figure out a way of getting the this pointer to derived functions

                    // handle moving symbols from scope to type class
                    switch (decl.Name)
                    {
                        case "variable_decl":
                            _visitVarDecl(decl, modifiers);
                            break;
                        case "func_decl":
                            _visitFunction(decl, modifiers);
                            break;
                        case "constructor_decl":
                            FunctionType ft = _visitConstructor(decl, modifiers);

                            if (!objType.AddConstructor(ft, modifiers.Count > 0))
                                throw new SemanticException("Unable to distinguish between constructor signatures", decl.Content[2].Position);

                            needsDefaultConstr = false;
                            break;
                        case "method_template":
                            _visitTemplate(decl, modifiers);
                            break;
                    }

                    MergeToBlock();
                }
            }

            var symbols = _table.GetScope();

            foreach (var sym in symbols)
                objType.AddMember(sym);

            for (int i = 0; i < interfaces.Count; i++)
            {
                var inter = interfaces[i];

                if (inter.Derive(objType))
                    objType.AddInherit(inter);
                else
                    throw new SemanticException("This type class does not implement all of its interfaces", ((ASTNode)node.Content[2]).Content[i * 2 + 1].Position);
            }

            // give objects a default constructor (if necessary)
            if (needsDefaultConstr)
                objType.AddConstructor(new FunctionType(new List<Parameter>(), new SimpleType(), false), false);

            _nodes.Add(new IdentifierNode(name, objType, true));

            _table.AscendScope();

            if (!_table.AddSymbol(new Symbol(name, objType, typeModifiers)))
                throw new SemanticException($"Unable to redeclare symbol {name}", node.Content[1].Position);

            _nodes.Add(new ExprNode("Implements", new SimpleType()));

            for (int i = 0; i < interfaces.Count; i++)
            {
                var inter = interfaces[i];

                _nodes.Add(new IdentifierNode(((TokenNode)((ASTNode)node.Content[2]).Content[i * 2 + 1]).Tok.Value, inter, true));
                MergeBack();
            }

            MergeBack(2);
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

        private FunctionType _visitConstructor(ASTNode decl, List<Modifier> modifiers)
        {
            List<Parameter> args = new List<Parameter>();

            foreach (var item in decl.Content)
            {
                if (item.Name == "args_decl_list")
                {
                    args = _generateArgsDecl((ASTNode)item);

                    _nodes.Add(new BlockNode("Constructor"));
                }
                else if (item.Name == "func_body")
                {
                    _nodes.Add(new IncompleteNode((ASTNode)item));
                    MergeToBlock();
                }
            }

            FunctionType ft = new FunctionType(args, new SimpleType(), false);

            _nodes.Add(new ValueNode("ConstructorSignature", ft));
            MergeBack();

            return ft;
        }
    }
}
