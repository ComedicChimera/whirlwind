using Whirlwind.Parser;

namespace Whirlwind.Generator.Visitor
{
    partial class Visitor
    {
        private void _visitExpr(ASTNode node)
        {
            foreach (var subNode in node.Content)
            {
                if (subNode.Name() == "atom")
                    _visitAtom((ASTNode)subNode);
                else if (subNode.Name() != "TOKEN")
                    _visitExpr((ASTNode)subNode);
            }
        }
    }
}
