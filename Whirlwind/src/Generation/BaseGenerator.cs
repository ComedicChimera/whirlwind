using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Globalization;

using LLVMSharp;

using Whirlwind.Semantic;
using Whirlwind.Types;

namespace Whirlwind.Generation
{
    partial class Generator
    {
        private LLVMValueRef _generateExprValue(ValueNode node)
        {
            switch (node.Name)
            {
                case "Literal":
                    {
                        var st = (SimpleType)node.Type;

                        switch (st.Type)
                        {
                            case SimpleType.SimpleClassifier.BOOL:
                                return LLVM.ConstInt(_convertType(node.Type), (ulong)(node.Value == "true" ? 1 : 0), new LLVMBool(0));
                            case SimpleType.SimpleClassifier.CHAR:
                                return LLVM.ConstInt(LLVM.Int32Type(), _convertCharLiteral(node.Value), new LLVMBool(0));
                            case SimpleType.SimpleClassifier.SHORT:
                            case SimpleType.SimpleClassifier.INTEGER:
                            case SimpleType.SimpleClassifier.LONG:
                                return LLVM.ConstIntOfString(_convertType(node.Type), node.Value.TrimEnd('u', 'l'), 10);
                            case SimpleType.SimpleClassifier.FLOAT:
                            case SimpleType.SimpleClassifier.DOUBLE:
                                return LLVM.ConstRealOfString(_convertType(node.Type), node.Value.TrimEnd('d'));
                            case SimpleType.SimpleClassifier.STRING:
                                {
                                    var arrData = _convertString(node.Value);
                                    var arrType = LLVM.ArrayType(LLVM.Int32Type(), (uint)arrData.Length);

                                    return LLVM.ConstNamedStruct(_stringType, new[]
                                    {
                                        LLVM.BuildGEP(_builder,
                                            LLVM.BuildArrayAlloca(
                                                _builder, arrType,
                                                LLVM.ConstArray(LLVM.Int32Type(), arrData),
                                                "string_arr_tmp"
                                            ),
                                            new[] {
                                                LLVM.ConstInt(LLVM.Int64Type(), 0, new LLVMBool(0)),
                                                LLVM.ConstInt(LLVM.Int64Type(), 0, new LLVMBool(0))
                                            },
                                            "string_tmp"
                                            ),
                                        LLVM.ConstInt(LLVM.Int32Type(), (uint)node.Value.Length - 2, new LLVMBool(0))
                                    });
                                }
                        }
                    }
                    break;
                case "This":
                    return _getNamedValue("this");
                case "Value":
                    return _getNamedValue("value_tmp");
                case "ByteLiteral":
                    {
                        ulong val = node.Value.StartsWith("0x") ? Convert.ToUInt64(node.Value, 16) : Convert.ToUInt64(node.Value, 2);

                        return LLVM.ConstInt(_convertType(node.Type), val, new LLVMBool(0));
                    }
            }

            // other values a bit more complicated
            return _ignoreValueRef();
        }

        Dictionary<uint, uint> _charTranslationDict = new Dictionary<uint, uint>
        {
            { 97, 7 },
            { 98, 8 },
            { 102, 12 },
            { 110, 10 },
            { 116, 9 },
            { 118, 11 },
            { 115, 10 },
            { 39, 39 },
            { 48, 0 }
        };

        private LLVMValueRef[] _convertString(string stringLit)
        {
            string stringData = stringLit.Substring(1, stringLit.Length - 2);

            var intData = new List<uint>();

            TextElementEnumerator e = StringInfo.GetTextElementEnumerator(stringData);
            byte[] bytes;
            while (e.MoveNext())
            {
                bytes = Encoding.UTF8.GetBytes(stringData, e.ElementIndex, 1);

                if (bytes.Length == 1)
                {
                    byte c = bytes[0];
                    int take = 0;

                    if (c == 92)
                    {
                        e.MoveNext();

                        // should always be good
                        c = Encoding.UTF8.GetBytes(stringData, e.ElementIndex, 1)[0];

                        uint unicodeInt = 0;
                        if (c == 85)
                            take = 8;
                        else if (c == 117)
                            take = 4;
                        else
                        {
                            if (_charTranslationDict.ContainsKey(c))
                                unicodeInt = _charTranslationDict[c];
                            else
                                unicodeInt = c;
                        }
                        
                        for(; take > 0; take--)
                        {
                            unicodeInt *= 16;

                            e.MoveNext();

                            c = Encoding.UTF8.GetBytes(stringData, e.ElementIndex, 1)[0];

                            if (c < 58)
                                unicodeInt += (uint)(c - 48);
                            else
                                unicodeInt += (uint)(c - 55);
                        }

                        if (unicodeInt > 1114112)
                            throw new GeneratorException("Invalid unicode point: " + stringData);

                        intData.Add(unicodeInt);
                        continue;
                    }
                }

                byte[] intBytes = new byte[sizeof(int)];
                Array.Copy(bytes, intBytes, bytes.Length);

                intData.Add(BitConverter.ToUInt32(intBytes));
            }

            return intData.Select(x => LLVM.ConstInt(LLVM.Int32Type(), x, new LLVMBool(0))).ToArray();
        }

        private uint _convertCharLiteral(string charLit)
        {
            string charData = charLit.Substring(1, charLit.Length - 2);

            // escape codes
            if (charData.StartsWith('\''))
            {
                switch (charData[1])
                {
                    case 'a':
                        return 7;
                    case 'b':
                        return 8;
                    case 'f':
                        return 12;
                    case 'n':
                        return 10;
                    case 't':
                        return 9;
                    case 'v':
                        return 11;
                    case 's':
                        return 32;
                    case '\'':
                        return 39;
                    case '\"':
                        return 34;
                    case '\\':
                        return 92;
                    case 'u':
                    case 'U':
                        return Convert.ToUInt32(String.Concat(charData.Skip(2)), 16);
                    // null terminator (\0)
                    default:
                        return 0;
                }
            }

            byte[] textBytes = Encoding.UTF8.GetBytes(charData);
            byte[] numBytes = new byte[sizeof(int)];
            Array.Copy(textBytes, numBytes, textBytes.Length);
            return BitConverter.ToUInt32(numBytes, 0);
        }
    }
}
