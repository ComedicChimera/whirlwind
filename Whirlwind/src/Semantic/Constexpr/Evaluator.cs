using Whirlwind.Types;

namespace Whirlwind.Semantic.Constexpr
{
    class Evaluator
    {
        public static ValueNode Evaluate(TreeNode node)
        {
            return new ValueNode("blank", new SimpleType(SimpleType.DataType.NULL));
        }

        public static bool TryEval(TreeNode node)
        {
            return false;
        }
    }
}
