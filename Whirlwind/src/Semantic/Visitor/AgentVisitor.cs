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

            foreach (var item in node.Content)
            {
                switch (item.Name)
                {
                    case "TOKEN":
                        if (((TokenNode)item).Tok.Type == "IDENTIFIER")
                            _nodes.Add(new IdentifierNode(((TokenNode)item).Tok.Value, exprType, true));
                        break;
                    case "expr":
                        _visitExpr((ASTNode)item);
                        break;
                    case "block":
                        MergeBack(2);

                        _nodes.Add(new IncompleteNode((ASTNode)item));
                        MergeToBlock();
                        break;
                }
            }
        }
    }
}
