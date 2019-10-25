using System;
using System.Collections.Generic;
using System.Linq;

using LLVMSharp;

using Whirlwind.Types;
using Whirlwind.Semantic;

namespace Whirlwind.Generation
{
    partial class Generator
    {
        private LLVMTypeRef _convertType(DataType dt)
        {
            if (dt is SimpleType simt)
            {
                switch (simt.Type)
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
            else if (dt is ArrayType at)
            {
                ((GenericType)_typeImpls["array"]).CreateGeneric(new List<DataType> { at.ElementType }, out DataType ast);
                return _convertType(ast);
            }
            else if (dt is ListType lt)
            {
                ((GenericType)_typeImpls["list"]).CreateGeneric(new List<DataType> { lt.ElementType }, out DataType lst);
                return _convertType(lst);
            }
            else if (dt is DictType dct)
            {
                ((GenericType)_typeImpls["dict"]).CreateGeneric(new List<DataType> { dct.KeyType, dct.ValueType }, out DataType dst);
                return _convertType(dst);
            }
            else if (dt is PointerType pt)
                return LLVM.PointerType(_convertType(pt.DataType), 0);
            else if (dt is StructType st)
            {
                string lName = _getLookupName(st.Name);

                Symbol symbol = null;
                foreach (var item in lName.Split("::"))
                    _table.Lookup(item, out symbol);

                if (symbol.DataType is StructType)
                    return LLVM.GetTypeByName(_module, st.Name);
                // only other option is generic type
                else
                    return _processGeneric((GenericType)symbol.DataType, dt);
            }
            else if (dt is TupleType tt)
                return LLVM.StructType(tt.Types.Select(x => _convertType(x)).ToArray(), true);
            else if (dt is FunctionType ft)
            {
                return LLVM.PointerType(LLVM.FunctionType(_convertType(ft.ReturnType),
                    ft.Parameters.Select(x => _convertType(x.DataType)).ToArray(),
                    ft.Parameters.Count > 0 && ft.Parameters.Last().Indefinite), 0);
            }
            else
                return LLVM.VoidType();
        }

        private LLVMTypeRef _processGeneric(GenericType gt, DataType ot)
        {
            return LLVM.VoidType();
        }

        private LLVMValueRef _coerce(LLVMValueRef val, DataType start, DataType desired)
        {
            if (start is SimpleType st)
            {

            }

            return _ignoreValueRef();
        }

        private LLVMValueRef _cast(LLVMValueRef val, DataType start, DataType desired)
        {
            return _ignoreValueRef();
        }
    }
}
