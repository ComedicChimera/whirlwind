using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

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
                                    var arrType = LLVM.ArrayType(LLVM.Int32Type(), (uint)node.Value.Length - 1);

                                    return LLVM.ConstNamedStruct(_stringType, new[]
                                    {
                                        LLVM.BuildGEP(_builder,
                                            LLVM.BuildArrayAlloca(
                                                _builder, arrType,
                                                LLVM.ConstArray(LLVM.Int32Type(), _convertString(node.Value)),
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

            return new LLVMValueRef[1];
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
