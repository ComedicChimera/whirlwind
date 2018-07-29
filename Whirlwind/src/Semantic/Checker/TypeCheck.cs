using Whirlwind.Types;

namespace Whirlwind.Semantic.Checker
{
    static partial class Checker
    {
        // accounts for references and other abnormal types
        public static bool CheckClassification(IDataType dt, string desiredClass)
        {
            if (dt.Classify() == desiredClass)
                return true;
            else if (dt.Classify() == "REFERENCE")
            {
                if (((ReferenceType)dt).Type.Classify() == desiredClass)
                    return true;
            }
            return false;
        }

        // accounts for references
        public static bool CheckCoercion(IDataType start, IDataType desired)
        {
            if (desired.Coerce(start))
                return true;
            else if (start.Classify() == "REFERENCE")
            {
                if (desired.Coerce(((ReferenceType)start).Type))
                    return true;
            }
            return false;
        }
    }
}
