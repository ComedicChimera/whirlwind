using Whirlwind.Types;

namespace Whirlwind.Semantic.Constexpr
{
    class Evaluator
    {
        public static ValueNode Evaluate(ExprNode node)
        {
            return new ValueNode("blank", new SimpleType());
        }

        public static bool TryEval(ExprNode node)
        {
            return false;
        }
    }
}
