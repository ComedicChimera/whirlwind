using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Whirlwind.Semantic.Checker
{
    static partial class Checker
    {
        // handle unicode in accordance with table
        // (https://lemire.me/blog/2018/05/09/how-quickly-can-you-check-that-a-string-is-valid-unicode-utf-8/)
        private static byte[][][] _threeByteCharRanges = 
        {
            new[] { new byte[] { 0xE0, 0xFF }, new byte[] { 0xA0, 0xBF }, new byte[] { 0x80, 0xBF } },
            new[] { new byte[] { 0xE1, 0xEC }, new byte[] { 0x80, 0xBF }, new byte[] { 0x80, 0xBF } },
            new[] { new byte[] { 0xED, 0xFF }, new byte[] { 0x80, 0x9F }, new byte[] { 0x80, 0xBF } },
            new[] { new byte[] { 0xEE, 0xEF }, new byte[] { 0x80, 0xBF }, new byte[] { 0x80, 0xBF } }
        };

        private static byte[][][] _fourByteCharRanges =
        {
            new[] { new byte[] { 0xF0, 0xFF }, new byte[] { 0x90, 0xBF }, new byte[] { 0x80, 0xBF}, new byte[] { 0x80, 0xBF }},
            new[] { new byte[] { 0xF1, 0xF3 }, new byte[] { 0x80, 0xBF }, new byte[] { 0x80, 0xBF}, new byte[] { 0x80, 0xBF }},
            new[] { new byte[] { 0xF4, 0xFF }, new byte[] { 0x80, 0x8F }, new byte[] { 0x80, 0xBF}, new byte[] { 0x80, 0xBF }}
        };

        public static bool VerifyChar(string charLit)
        {
            string charData = charLit.Substring(1, charLit.Length - 2);

            if (charData.Length == 1)
                return true;
            else if (charData.StartsWith("\\"))
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
            else if (charData.Length == 2)
            {
                byte[] bytes = Encoding.UTF8.GetBytes(charData);

                if (bytes.Length == 3)
                {
                    foreach (var arr in _threeByteCharRanges)
                    {
                        for (int i = 0; i < 3; i++)
                        {
                            if (arr[i][0] <= bytes[i] && bytes[i] <= arr[i][1])
                                return true;
                        }
                    }
                }
                else
                {
                    foreach (var arr in _fourByteCharRanges)
                    {
                        for (int i = 0; i < 4; i++)
                        {
                            if (arr[i][0] <= bytes[i] || bytes[i] <= arr[i][1])
                                return true;
                        }
                    }
                }
            }
            
            return false;
        }

        public static bool VerifyString(string stringLit)
        {
            int expectHex = 0;
            bool expectEscapeChar = false;

            string hexChars = "0123456789ABCDEF";
            string escapeChars = "ab0fntv\"\'\\s";

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
