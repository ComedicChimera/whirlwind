using Whirlwind.Types;

namespace Whirlwind.Semantic.Checker
{
    static partial class Checker
    {
        public static bool TypeCast(IDataType start, IDataType desired)
        {
            return false;
        }

        public static IDataType ValueCast(IDataType baseType)
        {
            return baseType;
        }
    }
}
