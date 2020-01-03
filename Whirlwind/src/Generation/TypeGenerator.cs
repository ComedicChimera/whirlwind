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
                    case SimpleType.SimpleClassifier.SHORT:
                        return LLVM.Int16Type();
                    case SimpleType.SimpleClassifier.CHAR:
                    case SimpleType.SimpleClassifier.INTEGER:
                        return LLVM.Int32Type();
                    case SimpleType.SimpleClassifier.LONG:
                        return LLVM.Int64Type();
                    case SimpleType.SimpleClassifier.FLOAT:
                        return LLVM.FloatType();
                    case SimpleType.SimpleClassifier.DOUBLE:
                        return LLVM.DoubleType();
                    default:
                        return _stringType;
                }
            }
            else if (dt is ArrayType at)
            {
                ((GenericType)_impls["array"]).CreateGeneric(new List<DataType> { at.ElementType }, out DataType ast);
                return _convertType(ast);
            }
            else if (dt is ListType lt)
            {
                ((GenericType)_impls["list"]).CreateGeneric(new List<DataType> { lt.ElementType }, out DataType lst);
                return _convertType(lst);
            }
            else if (dt is DictType dct)
            {
                ((GenericType)_impls["dict"]).CreateGeneric(new List<DataType> { dct.KeyType, dct.ValueType }, out DataType dst);
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
                    return _globalStructs[lName];
                // only other option is generic type
                else
                    return _processGeneric((GenericType)symbol.DataType, dt);
            }
            else if (dt is InterfaceType it)
            {
                // interface types are effectively structs from llvm's perspective
                string lName = _getLookupName(it.Name);

                Symbol symbol = null;
                foreach (var item in lName.Split("::"))
                    _table.Lookup(item, out symbol);

                if (symbol.DataType is InterfaceType)
                    return _globalStructs[lName];
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
            else if (dt is CustomInstance ci)
            {
                var parent = ci.Parent;

                if (parent.Instances.Count == 1)
                {
                    var onlyInstance = parent.Instances[0];

                    if (onlyInstance is CustomAlias ca)
                        return _convertType(ca.Type);
                    else if (onlyInstance is CustomNewType cnt)
                        return cnt.Values.Count == 0 ? LLVM.Int16Type() : _globalStructs[parent.Name];
                }
                else if (parent.Instances.All(x => x is CustomNewType cnt && cnt.Values.Count == 0))
                    return LLVM.Int16Type();
                else
                    return _globalStructs[parent.Name];
            }
            
            return LLVM.VoidType();
        }

        private LLVMTypeRef _processGeneric(GenericType gt, DataType ot)
        {
            
            return LLVM.VoidType();
        }

        // any coercion also maps to a cast in this context
        private LLVMValueRef _cast(LLVMValueRef val, DataType start, DataType desired)
        {
            // interfaces introduce different casting rules
            if (desired is InterfaceType dInterf)
            {

            }

            if (start is SimpleType sst)
            {
                if (desired is SimpleType dst)
                    return _castSimple(val, sst, dst);
                else if (desired is PointerType dpt)
                {

                }
            }

            return _ignoreValueRef();
        }

        private byte _getSimpleClass(SimpleType st)
        {
            switch (st.Type)
            {
                case SimpleType.SimpleClassifier.BOOL:
                case SimpleType.SimpleClassifier.BYTE:
                case SimpleType.SimpleClassifier.CHAR:
                case SimpleType.SimpleClassifier.SHORT:
                case SimpleType.SimpleClassifier.INTEGER:
                case SimpleType.SimpleClassifier.LONG:
                    return 0;
                case SimpleType.SimpleClassifier.FLOAT:
                case SimpleType.SimpleClassifier.DOUBLE:
                    return 1;
                // string
                default:
                    return 2;
            }
        }

        private LLVMValueRef _castSimple(LLVMValueRef val, SimpleType sst, SimpleType dst)
        {
            // either constancy or signed conflict
            // neither of which result in a cast
            if (sst.Type == dst.Type)
                return val;         

            int lcs = _getSimpleClass(sst), lcd = _getSimpleClass(dst);

            if (lcs == lcd)
            {
                // integral to integral
                if (lcs == 0)
                {
                    // downcast
                    if ((int)sst.Type > (int)dst.Type)
                        return LLVM.BuildTrunc(_builder, val, _convertType(dst), "cast_tmp");
                    // otherwise pick which upcast to use
                    else if (sst.Unsigned || dst.Unsigned)
                        return LLVM.BuildZExt(_builder, val, _convertType(dst), "cast_tmp");
                    else
                        return LLVM.BuildSExt(_builder, val, _convertType(dst), "cast_tmp");
                }
                // floating to floating
                else if (lcs == 1)
                {
                    // know same casts have been eliminated so must be float to double
                    if (sst.Type == SimpleType.SimpleClassifier.FLOAT)
                        return LLVM.BuildFPExt(_builder, val, LLVM.DoubleType(), "cast_tmp");
                    // vice versa
                    else if (sst.Type == SimpleType.SimpleClassifier.DOUBLE)
                        return LLVM.BuildFPTrunc(_builder, val, LLVM.FloatType(), "cast_tmp");
                }
            }
            // integral to floating
            else if (lcs == 0 && lcd == 1)
            {
                if (sst.Unsigned)
                    return LLVM.BuildUIToFP(_builder, val, _convertType(dst), "cast_tmp");
                else
                    return LLVM.BuildSIToFP(_builder, val, _convertType(dst), "cast_tmp");
            }
            // floating to integral
            else if (lcs == 1 && lcd == 0)
            {
                if (dst.Unsigned)
                    return LLVM.BuildFPToUI(_builder, val, _convertType(dst), "cast_tmp");
                else
                    return LLVM.BuildFPToSI(_builder, val, _convertType(dst), "cast_tmp");
            }
            // integral to string
            else if (lcs == 0 && lcd == 2)
            {
                // only integral that works is char

            }
            // string to integral
            else
            {

            }

            // for now
            return _ignoreValueRef();
        }
    }
}
