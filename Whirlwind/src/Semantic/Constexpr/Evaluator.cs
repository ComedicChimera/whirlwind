using Whirlwind.Types;

namespace Whirlwind.Semantic.Constexpr
{
    class Evaluator
    {
        public static ValueNode Evaluate(ITypeNode node)
        {
            return new ValueNode("blank", new SimpleType());
        }

        public static bool TryEval(ITypeNode node)
        {
            return false;
        }
    }
}
