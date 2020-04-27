using System.Collections.Generic;
using System.Linq;

using Whirlwind.Syntax;
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

            var typeClass = new CustomType(_namePrefix + name);
            _nodes.Add(new IdentifierNode(name, typeClass));
            MergeBack();

            // self referential pointer declaration
            _table.AddSymbol(new Symbol(name, new SelfType(_namePrefix + name, typeClass) { Constant = true }));

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

                            typeClass.AddInstance(new CustomAlias(typeClass, dt));
                        }
                    }
                    else
                    {
                        string newTypeName = ((TokenNode)decl.Content[0]).Tok.Value;

                        var values = new List<DataType>();

                        foreach (var elem in ((ASTNode)decl.Content[1]).Content)
                        {
                            if (elem.Name == "types")
                            {
                                ASTNode anode = (ASTNode)elem;

                                var dt = _generateType(anode);
                                values.Add(dt);

                                _nodes.Add(new ValueNode("Type", dt));
                            }
                        }

                        var ct = new CustomNewType(typeClass, newTypeName, values);

                        if (!typeClass.AddInstance(ct))
                            throw new SemanticException("Members of type class must be distinguishable", decl.Position);

                        _nodes.Add(new ExprNode("NewType", ct));
                        PushForward(values.Count);
                    }
                }
            }

            _table.AscendScope();

            if (!_table.AddSymbol(new Symbol(name, typeClass, typeModifiers)))
                throw new SemanticException($"Unable to redeclare symbol: `{name}`", node.Content[1].Position);

            if (genericVars.Count > 0)
                _makeGeneric(node, genericVars, typeModifiers, _table.GetScope().Last(), node.Content[1].Position);

            // merge in all instances
            MergeBack(typeClass.Instances.Count);
        }
    }
}
