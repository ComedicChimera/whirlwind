using System.Collections.Generic;
using System.Linq;

using Whirlwind.Parser;
using Whirlwind.Types;

namespace Whirlwind.Semantic.Visitor
{
    partial class Visitor
    {
        private void _visitAgent(ASTNode node)
        {
            _nodes.Add(new BlockNode("Agent"));

            _table.AddScope();
            _table.DescendScope();

            string name = "";
            List<IDataType> evhTypes = new List<IDataType>();

            foreach (var item in node.Content)
            {
                if (item.Name == "TOKEN" && ((TokenNode)item).Tok.Type == "IDENTIFIER")
                    name = ((TokenNode)item).Tok.Value;
                else if (item.Name == "agent_main")
                {
                    ASTNode agentNode = (ASTNode)((ASTNode)item).Content[0];

                    switch (agentNode.Name)
                    {
                        case "func_decl":
                            _visitFunction(agentNode, new List<Modifier> { Modifier.CONSTANT });
                            break;
                        case "variable_decl":
                            _visitVarDecl(agentNode, new List<Modifier> { Modifier.CONSTANT });
                            break;
                        case "evh_decl":
                            _visitEventDecl(agentNode);
                            evhTypes.Add(((BlockNode)_nodes.Last()).Nodes[0].Type);
                            break;
                        case "method_template":
                            _visitTemplate(agentNode, new List<Modifier> { Modifier.CONSTANT });
                            break;
                    }

                    MergeToBlock();
                }
            }

            var agType = new AgentType(name, _table.GetScope(), evhTypes);

            _table.AscendScope();

            _nodes.Add(new IdentifierNode(name, agType, true));
            MergeBack();

            if (!_table.AddSymbol(new Symbol(name, agType, new List<Modifier> { Modifier.CONSTANT })))
                throw new SemanticException("Unable to redefine symbol", node.Content[1].Position);
        }
        
        private void _visitEventDecl(ASTNode node)
        {
            _nodes.Add(new BlockNode("EventHandler"));
            IDataType exprType = new SimpleType();

            _table.AddScope();
            _table.DescendScope();

            foreach (var item in node.Content)
            {
                switch (item.Name)
                {
                    case "evh_val":
                        {
                            ASTNode evhVal = (ASTNode)item;

                            string idName = ((TokenNode)evhVal.Content[2]).Tok.Value;
                            _visitExpr((ASTNode)evhVal.Content[0]);

                            _nodes.Add(new IdentifierNode(idName, _nodes.Last().Type, true));

                            if (idName != "_")
                                // since it is the first item in scope, no check necessary
                                _table.AddSymbol(new Symbol(idName, _nodes.Last().Type, new List<Modifier> { Modifier.CONSTANT }));

                            _nodes.Add(new ExprNode("ValueHandler", _nodes.Last().Type));
                            PushForward(2);
                        }
                        break;
                    case "evh_cond":
                        {
                            ASTNode evhCond = (ASTNode)item;

                            string idName = ((TokenNode)evhCond.Content[0]).Tok.Value;
                            IDataType dt = _generateType((ASTNode)evhCond.Content[2]);

                            _nodes.Add(new IdentifierNode(idName, dt, true));
                            // since it is the first item in scope, no check necessary
                            _table.AddSymbol(new Symbol(idName, dt, new List<Modifier> { Modifier.CONSTANT }));

                            _visitExpr((ASTNode)evhCond.Content[4]);

                            if (!new SimpleType(SimpleType.DataType.BOOL).Coerce(_nodes.Last().Type))
                                throw new SemanticException("Condition must be evaluate to a boolean", evhCond.Content[4].Position);

                            _nodes.Add(new ExprNode("ConditionHandler", dt));
                            PushForward(2);
                        }
                        break;
                    case "block":
                        // merge the handler expr back
                        MergeBack();

                        _nodes.Add(new IncompleteNode((ASTNode)item));
                        MergeToBlock();
                        break;
                }
            }

            _table.AscendScope();
        }
    }
}
