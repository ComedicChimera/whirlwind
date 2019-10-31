using System.Collections.Generic;
using System.Globalization;
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
            else
            {
                byte[] bytes = Encoding.UTF8.GetBytes(charData);

                if (bytes.Length == 1)
                    return true;
                else if (bytes.Length == 2)
                    return bytes[0] >= 0xC2;
                
                // handle unicode in accordance with table
                // (https://lemire.me/blog/2018/05/09/how-quickly-can-you-check-that-a-string-is-valid-unicode-utf-8/)
                else
                    return false;
            }
        }

        public static bool VerifyString(string stringLit)
        {
            int expectHex = 0;
            bool expectEscapeChar = false;

            string hexChars = "0123456789ABCDEF";
            string escapeChars = "ab0fntv\"\'\\0s";

            foreach (char ch in stringLit.Substring(1, stringLit.Length - 1))
            {
                if (expectEscapeChar)
                {
                    if (ch == 'u')
                        expectHex = 4;
                    else if (ch == 'U')
                        expectHex = 8;
                    else if (!escapeChars.Contains(ch))
                        return false;
                }
                else if (expectHex > 0)
                {
                    if (!hexChars.Contains(ch))
                        return false;

                    expectHex--;
                }
                else if (ch == '\\')
                    expectEscapeChar = true;
            }

            return true;
        }
    }
}
