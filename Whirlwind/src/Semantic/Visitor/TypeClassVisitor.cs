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

            // self referential pointer declaration
            _table.AddSymbol(new Symbol(name, new SelfType(_namePrefix + name, typeClass) { Constant = true }));

            foreach (var item in node.Content)
            {
                if (item.Name == "type_class_main")
                {
                    var decl = (ASTNode)item;

                    if (decl.Content[0].Name == "type_id")
                    {
                        var typeId = (ASTNode)decl.Content[0];
                        var types = (ASTNode)typeId.Content.Last();

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
                                if (typeId.Content.Count > 1)
                                {
                                    _table.AddScope();
                                    _table.DescendScope();

                                    var resIdName = ((TokenNode)typeId.Content[0]).Tok.Value;

                                    _table.AddSymbol(new Symbol(resIdName, dt));

                                    _nodes.Add(new IdentifierNode(resIdName, dt));
                                    _visitValueRestrictor((ASTNode)decl.Content[1]);
                                    MergeBack(2);

                                    _table.AscendScope();
                                }
                                else
                                {
                                    _visitValueRestrictor((ASTNode)decl.Content[1]);
                                    MergeBack();
                                }                                                                    
                           }
                           else if (typeId.Content.Count > 1)
                                throw new SemanticException("Unable to declare type placeholder without value restrictor", typeId.Content[0].Position);

                            typeClass.AddInstance(new CustomAlias(typeClass, dt));
                        }
                    }
                    else
                    {
                        string newTypeName = ((TokenNode)decl.Content[0]).Tok.Value;

                        var values = new List<DataType>();
                        var awaitingRestrictor = false;

                        foreach (var elem in ((ASTNode)decl.Content[1]).Content)
                        {
                            if (elem.Name == "type_id")
                            {
                                ASTNode anode = (ASTNode)elem;

                                var dt = _generateType((ASTNode)anode.Content.Last());
                                values.Add(dt);

                                if (anode.Content.Count > 1)
                                {
                                    if (!awaitingRestrictor)
                                    {
                                        _table.AddScope();
                                        _table.DescendScope();

                                        awaitingRestrictor = true;
                                    }

                                    var resIdName = ((TokenNode)anode.Content[0]).Tok.Value;

                                    if (!_table.AddSymbol(new Symbol(resIdName, dt)))
                                        throw new SemanticException($"Unable to redeclare type placeholder: `{resIdName}`", 
                                            anode.Content[0].Position);

                                    _nodes.Add(new IdentifierNode(resIdName, dt));
                                }
                                else
                                    _nodes.Add(new ValueNode("Type", dt));
                            }
                        }

                        var ct = new CustomNewType(typeClass, newTypeName, values);

                        if (!typeClass.AddInstance(ct))
                            throw new SemanticException("Members of type class must be distinguishable", decl.Position);

                        _nodes.Add(new ExprNode("NewType", ct));
                        PushForward(values.Count);

                        if (decl.Content.Last().Name == "value_restrictor")
                        {
                            _visitValueRestrictor((ASTNode)decl.Content.Last());
                            MergeBack();

                            _table.AscendScope();
                        }
                        else if (awaitingRestrictor)
                            throw new SemanticException("Unable to declare type placeholders without value restrictor", decl.Position);
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

        private void _visitValueRestrictor(ASTNode restrictor)
        {
            _visitExpr((ASTNode)restrictor.Content[1]);

            if (!new SimpleType(SimpleType.SimpleClassifier.BOOL).Coerce(_nodes.Last().Type))
                throw new SemanticException("Type of value restrictor expression must be a boolean", restrictor.Content[1].Position);

            _nodes.Add(new ExprNode("ValueRestrictor", new NoneType()));
            PushForward();
        }
    }
}
