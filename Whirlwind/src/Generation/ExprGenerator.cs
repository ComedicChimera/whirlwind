using System;
using System.Collections.Generic;
using System.Linq;

using LLVMSharp;

using Whirlwind.Semantic;
using Whirlwind.Types;

namespace Whirlwind.Generation
{
    partial class Generator
    {
        private LLVMValueRef _generateExpr(ITypeNode expr)
        {
            if (expr is ValueNode vn)
                return _generateExprValue(vn);

            return LLVM.ConstInt(LLVM.Int32Type(), 0, new LLVMBool(0));
        }

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
                                return LLVM.ConstIntOfString(LLVM.Int8Type(), node.Value, 10);
                            case SimpleType.SimpleClassifier.INTEGER:
                            case SimpleType.SimpleClassifier.LONG:
                                return LLVM.ConstIntOfString(_convertType(node.Type), node.Value.TrimEnd('u', 'l'), 10);
                            case SimpleType.SimpleClassifier.FLOAT:
                            case SimpleType.SimpleClassifier.DOUBLE:
                                return LLVM.ConstRealOfString(_convertType(node.Type), node.Value.TrimEnd('d'));
                            case SimpleType.SimpleClassifier.STRING:
                                // for now
                                return LLVM.ConstInt(LLVM.Int32Type(), 0, new LLVMBool(0));
                        }
                    }
                    break;
                case "This":
                    return _getNamedValue("$THIS");
                case "Value":
                    return _getNamedValue("$value_tmp");
                case "ByteLiteral":
                    {
                        ulong val = node.Value.StartsWith("0x") ? Convert.ToUInt64(node.Value, 16) : Convert.ToUInt64(node.Value, 2);

                        return LLVM.ConstInt(_convertType(node.Type), val, new LLVMBool(0));
                    }
            }

            // other values a bit more complicated
            return LLVM.ConstInt(LLVM.Int32Type(), 0, new LLVMBool(0));
        }
    }
}
