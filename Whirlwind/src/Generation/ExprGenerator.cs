using System;
using System.Collections.Generic;
using System.Text;

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
                            case SimpleType.SimpleClassifier.INTEGER:
                            case SimpleType.SimpleClassifier.LONG:
                                return LLVM.ConstInt(_convertType(node.Type), ulong.Parse(node.Value), new LLVMBool(Convert.ToInt32(st.Unsigned)));
                            case SimpleType.SimpleClassifier.FLOAT:
                            case SimpleType.SimpleClassifier.DOUBLE:
                                return LLVM.ConstRealOfString(_convertType(node.Type), node.Value);
                            case SimpleType.SimpleClassifier.STRING:
                                // for now
                                return LLVM.ConstInt(LLVM.Int32Type(), 0, new LLVMBool(0));
                        }
                    }
                    break;
            }

            // other values a bit more complicated
            return LLVM.ConstInt(LLVM.Int32Type(), 0, new LLVMBool(0));
        }
    }
}
