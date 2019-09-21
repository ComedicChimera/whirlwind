using System;
using System.Collections.Generic;
using System.Text;

using LLVMSharp;

using Whirlwind.Types;

namespace Whirlwind.Generation
{
    partial class Generator
    {
        private LLVMTypeRef _convertType(DataType dt)
        {
            if (dt is SimpleType st)
            {
                switch (st.Type)
                {
                    case SimpleType.SimpleClassifier.BOOL:
                        return LLVM.Int1Type();
                    case SimpleType.SimpleClassifier.BYTE:
                        return LLVM.Int8Type();
                    case SimpleType.SimpleClassifier.CHAR:
                        return LLVM.Int16Type();
                    case SimpleType.SimpleClassifier.INTEGER:
                        return LLVM.Int32Type();
                    case SimpleType.SimpleClassifier.LONG:
                        return LLVM.Int64Type();
                    case SimpleType.SimpleClassifier.FLOAT:
                        return LLVM.FloatType();
                    case SimpleType.SimpleClassifier.DOUBLE:
                        return LLVM.DoubleType();
                    // handle strings later
                    default:
                        return LLVM.VoidType();
                }
            }
            else
                return LLVM.VoidType();
        }
    }
}
