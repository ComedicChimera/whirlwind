using Whirlwind.Types;

using System.Linq;

namespace Whirlwind.Semantic.Checker
{
    // check all special type interfaces
    static partial class Checker
    {
        public static bool Hashable(IDataType dt)
        {
            // add body
            return true;
        }

        public static bool Iterable(IDataType dt)
        {
            // add body
            return true;
        }

        /* Check if a given node is modifiable
         * not a direct inferface mirror and really more predicated on checking constants
         * but it somewhat fits with the rest of these
         * 
         * Checks if the last item in the TNCL is modifiable
         */
        public static bool Modifiable()
        {
            // add body
            return true;
        }

        // also not technically an interface, but it exhibits similar behavior
        // checks if it is a number type (int, float, ect.)
        public static bool Numeric(IDataType dt)
        {
            if (dt.Classify() == "SIMPLE_TYPE")
            {
                return !new[] {
                        SimpleType.DataType.STRING, SimpleType.DataType.BYTE, SimpleType.DataType.BOOL, SimpleType.DataType.CHAR
                    }.Contains(((SimpleType)dt).Type);
            }
            return false;
        }
    }
}
