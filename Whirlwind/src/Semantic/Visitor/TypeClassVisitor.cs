using System.Collections.Generic;
using System.Linq;

using Whirlwind.Parser;
using Whirlwind.Types;

namespace Whirlwind.Semantic.Visitor
{
    partial class Visitor
    {
        private void _visitTypeClass(ASTNode node, List<Modifier> typeModifiers, List<GenericVariable> genericVars)
        {
            _nodes.Add(new BlockNode("TypeClass"));

            string name = ((TokenNode)node.Content[1]).Tok.Value;

            _table.AddScope();
            _table.DescendScope();

            var typeClass = new CustomType(name);

            // self referential pointer declaration
            _table.AddSymbol(new Symbol(name, new SelfType(typeClass) { Constant = true }));

            foreach (var item in node.Content)
            {
                if (item.Name == "type_class_main")
                {
                    var decl = (ASTNode)item;

                    if (decl.Content[0].Name == "types")
                    {
                        var types = (ASTNode)decl.Content[0];

                        if (types.Content[0] is TokenNode tn && tn.Tok.Type == "IDENTIFIER" 
                            && types.Content.Count == 1 && decl.Content.Count == 1 
                            && !_table.Lookup(tn.Tok.Value, out Symbol _))
                        {
                            var ct = new CustomNewType(typeClass, tn.Tok.Value, new List<DataType>());

                            // the only things that would match as types have no values
                            typeClass.AddInstance(ct);

                            _nodes.Add(new ExprNode("NewType", ct));
                        }
                        else
                        {
                            var dt = _generateType(types);

                            _nodes.Add(new ExprNode("AliasType", dt));

                            if (decl.Content.Count > 1)
                            {
                                _table.AddScope();
                                _table.DescendScope();

                                foreach (var elem in decl.Content.Skip(1))
                                {
                                    if (elem is TokenNode tn2 && tn2.Tok.Type == "IDENTIFIER")
                                    {
                                        _nodes.Add(new IdentifierNode(tn2.Tok.Value, dt));
                                        MergeBack();                                       

                                        // no check necessary (private sub scope)
                                        _table.AddSymbol(new Symbol(tn2.Tok.Value, dt));
                                    }
                                    else if (elem.Name == "expr")
                                    {
                                        _visitExpr((ASTNode)elem);
                                        MergeBack();
                                    }
                                }

                                _table.AscendScope();
                            }

                            typeClass.AddInstance(new CustomAlias(typeClass, dt));
                        }
                    }
                    else
                    {
                        string newTypeName = ((TokenNode)decl.Content[0]).Tok.Value;

                        var values = new List<DataType>();

                        if (decl.Content.Count == 2)
                            values = _generateTypeList((ASTNode)((ASTNode)decl.Content[1]).Content[1]);

                        var ct = new CustomNewType(typeClass, newTypeName, values);

                        if (!typeClass.AddInstance(ct))
                            throw new SemanticException("Members of type class must be distinguishable", decl.Position);

                        _nodes.Add(new ExprNode("NewType", ct));
                    }
                }
            }

            _table.AscendScope();

            if (!_table.AddSymbol(new Symbol(name, typeClass, typeModifiers)))
                throw new SemanticException($"Unable to redeclare symbol by name `{name}`", node.Content[1].Position);

            if (genericVars.Count > 0)
                _makeGeneric(node, genericVars, typeModifiers, node.Content[1].Position);

            // declare new types
            for (int i = 0; i < typeClass.Instances.Count; i++)
            {
                if (typeClass.Instances[i] is CustomNewType cnType)
                {
                    if (!_table.AddSymbol(new Symbol(cnType.Name, cnType)))
                        throw new SemanticException("All new type members of a type class must be declarable within the enclosing scope",
                            node.Content.Where(x => x.Name == "type_class_main").ElementAt(i).Position);
                }

                MergeBack();
            }
        }
    }
}
