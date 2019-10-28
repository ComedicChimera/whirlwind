using System;
using System.Collections.Generic;
using System.Text;

namespace Whirlwind.Semantic.Checker
{
    static partial class Checker
    {
        public static bool VerifyChar(string charLit)
        {
            string charData = charLit.Substring(1, charLit.Length - 2);

            if (charData.StartsWith("\\"))
            {
                if (charData.StartsWith("\\u"))
                    return charData.Length == 6;
                else if (charData.StartsWith("\\U"))
                    return charData.Length == 10;
                else if (charData.Length == 2)
                    return "abftvnr0s\"\'\\".Contains(charData[1]);
                else
                    return false;
            }
            // handle unicode strings
            else
                return charData.Length == 1;
        }

        public static bool VerifyString(string stringLit)
        {
            return true;
        }
    }
}
