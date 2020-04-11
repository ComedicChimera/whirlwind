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
                            case SimpleType.SimpleClassifier.BYTE: // for match expr
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
                                    var arrType = LLVM.ArrayType(LLVM.Int8Type(), (uint)arrData.Length);

                                    var strLitArr = LLVM.BuildAlloca(_builder, arrType, "string_lit_arr_tmp");

                                    uint i = 0;
                                    foreach (var elem in arrData)
                                    {
                                        var elemPtr = LLVM.BuildGEP(_builder, strLitArr,
                                            new[] {
                                                LLVM.ConstInt(LLVM.Int32Type(), 0, new LLVMBool(0)),
                                                LLVM.ConstInt(LLVM.Int32Type(), i, new LLVMBool(0))
                                            },
                                            "elem_ptr"
                                            );

                                        LLVM.BuildStore(_builder, elem, elemPtr);

                                        i++;
                                    }

                                    var strLitArrPtr = LLVM.BuildBitCast(_builder, strLitArr, _i8PtrType, "str_lit_arr_ptr_tmp");

                                    var strLit = LLVM.BuildAlloca(_builder, _stringType, "string_lit_tmp");

                                    var strLitArrPtrElem = LLVM.BuildStructGEP(_builder, strLit, 0, "string_lit_arr_ptr_elem_ptr_tmp");
                                    LLVM.BuildStore(_builder, strLitArrPtr, strLitArrPtrElem);

                                    var strLitByteCountElem = LLVM.BuildStructGEP(_builder, strLit, 1, "string_lit_bc_elem_ptr_tmp");
                                    LLVM.BuildStore(_builder, 
                                        LLVM.ConstInt(LLVM.Int32Type(), (ulong)arrData.Length, new LLVMBool(0)), strLitByteCountElem);

                                    var strLitLengthElem = LLVM.BuildStructGEP(_builder, strLit, 2, "string_lit_len_elem_ptr_tmp");
                                    LLVM.BuildStore(_builder,
                                        LLVM.ConstInt(LLVM.Int32Type(), (ulong)node.Value.Length - 2, new LLVMBool(0)), strLitLengthElem);

                                    return strLit;
                                }
                        }
                    }
                    break;
                case "Value":
                    return _getNamedValue("$VALUE").Vref;
                case "ByteLiteral":
                    {
                        ulong val = node.Value.StartsWith("0x") ? Convert.ToUInt64(node.Value, 16) : Convert.ToUInt64(node.Value, 2);

                        return LLVM.ConstInt(_convertType(node.Type), val, new LLVMBool(0));
                    }
                case "Null":
                    return _getNullValue(((NullType)node.Type).EvaluatedType);
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

        private uint _maxUnicodePoint = 1114112;

        private LLVMValueRef[] _convertString(string stringLit)
        {
            string stringData = stringLit.Substring(1, stringLit.Length - 2);

            byte[] bytes = Encoding.UTF8.GetBytes(stringData);

            return bytes.Select(x => LLVM.ConstInt(LLVM.Int8Type(), (ulong)x, new LLVMBool(0))).ToArray();
        }

        private uint _convertCharLiteral(string charLit)
        {
            string charData = charLit.Substring(1, charLit.Length - 2);

            // escape codes
            if (charData.StartsWith('\\'))
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
                        {
                            var unicodeInt = Convert.ToUInt32(String.Concat(charData.Skip(2)), 16);

                            if (unicodeInt > _maxUnicodePoint)
                                throw new GeneratorException("Invalid unicode point: " + charData);

                            return unicodeInt;
                        }
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

        private LLVMValueRef _getNullValue(DataType dt)
        {
            if (dt is SimpleType st)
            {
                if (st.Type == SimpleType.SimpleClassifier.STRING)
                    return _getNullStruct(_stringType, "__string");
                else
                    return LLVM.ConstNull(_convertType(dt));
            }
            else if (dt is PointerType)
                return LLVM.ConstPointerNull(_convertType(dt));
            else if (dt is StructType)
                return _getNullStruct(_convertType(dt), _getLookupName(dt));
            else if (dt is IIterableType)
            {
                var iterStructType = _convertType(dt);
                return _getNullStruct(iterStructType, _getLLVMStructName(iterStructType));
            }              

            return _ignoreValueRef();
        }

        private LLVMValueRef _getNullStruct(LLVMTypeRef nullStructDt, string structName)
        {
            var nullStruct = LLVM.BuildAlloca(_builder, nullStructDt, "nullstruct_" + structName);
            LLVM.BuildCall(_builder, _globalScope[structName + "._$initMembers"].Vref, new[] { nullStruct }, "");

            return nullStruct;
        }
    }
}
