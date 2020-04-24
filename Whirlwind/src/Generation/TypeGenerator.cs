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
        private LLVMTypeRef _convertType(DataType dt, bool usePtrTypes=false)
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
                        if (usePtrTypes)
                            return LLVM.PointerType(_stringType, 0);

                        return _stringType;
                }
            }
            else if (dt is ArrayType at)
                return _makeGenerate((GenericType)_impls["array"], usePtrTypes, at.ElementType);
            else if (dt is ListType lt)
                return _makeGenerate((GenericType)_impls["list"], usePtrTypes, lt.ElementType);
            else if (dt is DictType dct)
                return _makeGenerate((GenericType)_impls["dict"], usePtrTypes, dct.KeyType, dct.ValueType);
            else if (dt is PointerType pt)
                return LLVM.PointerType(_convertType(pt.DataType, true), 0);
            else if (dt is StructType st)
            {
                string lName = _getLookupName(st.Name);

                Symbol symbol = _getSymbolFromLookupName(lName);

                if (symbol.DataType is StructType)
                    return _getGlobalStruct(lName, usePtrTypes);
                // only other option is generic type
                else
                    return _processGeneric((GenericType)symbol.DataType, dt, usePtrTypes);
            }
            else if (dt is InterfaceType it)
            {
                // interface types are effectively structs from llvm's perspective
                string lName = _getLookupName(it.Name);

                Symbol symbol = _getSymbolFromLookupName(lName);

                if (symbol.DataType is InterfaceType)
                    return _getGlobalStruct(lName, usePtrTypes);
                // only other option is generic type
                else
                    return _processGeneric((GenericType)symbol.DataType, dt, usePtrTypes);
            }
            else if (dt is TupleType tt)
            {
                var tStruct = _createLLVMStructType(tt.Types);

                return usePtrTypes ? LLVM.PointerType(tStruct, 0) : tStruct;
            }
            else if (dt is FunctionType ft)
            {
                var parameters = ft.Parameters.Select(x => _convertType(x.DataType, true)).ToList();
                LLVMTypeRef rtType;

                if (_isReferenceType(ft.ReturnType))
                {
                    parameters.Insert(0, _convertType(ft.ReturnType, true));
                    rtType = LLVM.VoidType();
                }
                else
                    rtType = _convertType(ft.ReturnType);

                if (ft.IsBoxed)
                    parameters.Insert(0, _i8PtrType);

                var fp = LLVM.PointerType(LLVM.FunctionType(rtType, parameters.ToArray(),
                    ft.Parameters.Count > 0 && ft.Parameters.Last().Indefinite), 0);

                var functionStruct = LLVM.StructType(new[] { fp, _i8PtrType }, false);

                if (usePtrTypes)
                    return LLVM.PointerType(functionStruct, 0);

                return functionStruct;
            }
            else if (dt is CustomInstance ci)
            {
                var parent = ci.Parent;

                if (parent.Instances.Count == 1)
                {
                    var onlyInstance = parent.Instances[0];

                    if (onlyInstance is CustomAlias ca)
                        return _convertType(ca.Type, usePtrTypes);
                    else if (onlyInstance is CustomNewType cnt)
                        return cnt.Values.Count == 0 ? LLVM.Int16Type() : _getGlobalStruct(_getLookupName(parent.Name), usePtrTypes);
                }
                else if (parent.Instances.All(x => x is CustomNewType cnt && cnt.Values.Count == 0))
                    return LLVM.Int16Type();
                else
                    return _getGlobalStruct(_getLookupName(parent.Name), usePtrTypes);
            }
            else if (dt is AnyType)
            {
                if (usePtrTypes)
                    return LLVM.PointerType(_anyType, 0);

                return _anyType;
            }
            else if (dt is SelfType selfT)
                return _convertType(selfT.DataType, usePtrTypes); // should never be null
            else if (dt is GenericSelfInstanceType gsit)
                return _makeGenerate(gsit.GenericSelf, usePtrTypes, gsit.TypeList);

            return LLVM.VoidType();
        }

        private LLVMTypeRef _getGlobalStruct(string name, bool usePtrTypes)
        {
            var gStruct = _globalStructs[name];

            if (usePtrTypes)
                return LLVM.PointerType(gStruct, 0);

            return gStruct;
        }

        private LLVMTypeRef _makeGenerate(GenericType gt, bool usePtrTypes, List<DataType> typeArguments)
        {
            // assume this will work :D
            gt.CreateGeneric(typeArguments, out DataType generateType);
            var generate = gt.Generates.Single(x => x.Type.GenerateEquals(generateType));

            _genericSuffix = ".variant." + string.Join("_", generate.GenericAliases
                     .Values.Select(x => x.LLVMName()));

            var generateLookupName = _getLookupName(gt) + _genericSuffix;

            if (gt.Generates.First().Block.Name == "TypeGenerate")
                return _convertType(gt.Generates.First().Block.Nodes[0].Type);

            if (!_globalStructs.ContainsKey(generateLookupName))
            {
                switch (generateType.Classify())
                {
                    case TypeClassifier.STRUCT_INSTANCE:
                        _generateStruct(generate.Block, false);
                        break;
                    case TypeClassifier.INTERFACE_INSTANCE:
                        _generateInterf(generate.Block);
                        break;
                    case TypeClassifier.TYPE_CLASS_INSTANCE:
                        _generateTypeClass(generate.Block);
                        break;
                    // functions shouldn't be used here
                }              
            }
                

            var gVar = _getGlobalStruct(generateLookupName, usePtrTypes);
            _genericSuffix = "";

            return gVar;
        }

        // allow for creation of artificial struct generates
        private LLVMTypeRef _makeGenerate(GenericType gt, bool usePtrTypes, params DataType[] typeArguments)
            => _makeGenerate(gt, usePtrTypes, typeArguments.ToList());

        private LLVMTypeRef _processGeneric(GenericType gt, DataType ot, bool usePtrTypes)
        {
            foreach (var generate in gt.Generates)
            {
                if (generate.Type.GenerateEquals(ot))
                {
                    string baseName = _getLookupName(gt);

                    // all generic types of this form should have a global struct associated
                    return _getGlobalStruct(baseName + ".variant." + string.Join("_", generate.GenericAliases
                        .Values.Select(x => x.LLVMName())), usePtrTypes);                  
                }
            }

            return LLVM.VoidType();
        }

        // any coercion also maps to a cast in this context
        private LLVMValueRef _cast(LLVMValueRef val, DataType start, DataType desired)
        {
            // ignore constancy during check
            if (start.ConstCopy().Equals(desired.ConstCopy()))
                return val;

            // interfaces introduce different casting rules
            if (desired is InterfaceType dInterf)
            {
                var interf = _alloca(dInterf, "interf_box_tmp");
                var thisElemPtr = LLVM.BuildStructGEP(_builder, interf, 0, "this_elem_ptr_tmp");

                LLVMValueRef thisPtr;
                if (_isReferenceType(start) || start is PointerType)
                    thisPtr = LLVM.BuildBitCast(_builder, val, _i8PtrType, "this_ptr_tmp");
                else
                {
                    var castPtr = LLVM.BuildAlloca(_builder, LLVM.PointerType(_convertType(start), 0), "cast_ptr_tmp");
                    LLVM.SetAlignment(castPtr, WhirlGlobals.POINTER_SIZE);

                    LLVM.BuildStore(_builder, val, castPtr);

                    thisPtr = LLVM.BuildBitCast(_builder, castPtr, _i8PtrType, "this_ptr_tmp");
                }

                LLVM.BuildStore(_builder, thisPtr, thisElemPtr);

                var vtableElemPtr = LLVM.BuildStructGEP(_builder, interf, 1, "vtable_elem_ptr_tmp");
                LLVM.BuildStore(_builder, _createVtable(start.GetInterface(), dInterf), vtableElemPtr);

                var cValElemPtr = LLVM.BuildStructGEP(_builder, interf, 2, "c_val_elem_ptr_tmp");
                LLVM.BuildStore(_builder, LLVM.ConstInt(LLVM.Int16Type(), _getTypeCVal(start), new LLVMBool(0)), cValElemPtr);

                var sizeElemPtr = LLVM.BuildStructGEP(_builder, interf, 3, "size_elem_ptr_tmp");
                LLVM.BuildStore(_builder, LLVM.ConstInt(LLVM.Int32Type(), start.SizeOf(), new LLVMBool(0)), sizeElemPtr);

                return interf;
            }
            else if (desired is AnyType)
            {
                LLVMValueRef i8AnyValuePtr;

                if (_isReferenceType(start) || start is PointerType)
                    i8AnyValuePtr = LLVM.BuildBitCast(_builder, val, _i8PtrType, "cast_tmp");
                else
                {
                    var castPtr = LLVM.BuildAlloca(_builder, LLVM.PointerType(_convertType(start), 0), "cast_ptr_tmp");
                    LLVM.SetAlignment(castPtr, WhirlGlobals.POINTER_SIZE);

                    LLVM.BuildStore(_builder, val, castPtr);

                    i8AnyValuePtr = LLVM.BuildBitCast(_builder, castPtr, _i8PtrType, "cast_tmp");
                }

                var anyStruct = LLVM.BuildAlloca(_builder, _anyType, "any_struct_tmp");
                LLVM.SetAlignment(anyStruct, WhirlGlobals.POINTER_SIZE);

                var anyValElemPtr = LLVM.BuildStructGEP(_builder, anyStruct, 0, "any_val_elem_ptr_tmp");
                LLVM.BuildStore(_builder, i8AnyValuePtr, anyValElemPtr);

                var anyCValElemPtr = LLVM.BuildStructGEP(_builder, anyStruct, 1, "any_c_val_elem_ptr_tmp");
                LLVM.BuildStore(_builder, LLVM.ConstInt(LLVM.Int16Type(), _getTypeCVal(start), new LLVMBool(0)), anyCValElemPtr);

                var anySizeElemPtr = LLVM.BuildStructGEP(_builder, anyStruct, 2, "any_size_elem_ptr_tmp");
                LLVM.BuildStore(_builder, LLVM.ConstInt(LLVM.Int32Type(), start.SizeOf(), new LLVMBool(0)), anySizeElemPtr);

                return anyStruct;
            }
            // custom alias storage and casting

            if (start is SimpleType sst)
            {
                if (desired is SimpleType dst)
                    return _castSimple(val, sst, dst);
                else if (desired is PointerType)
                    return LLVM.BuildIntToPtr(_builder, val, _convertType(desired), "cast_tmp");
            }
            // TODO: ptr -> array
            else if (start is PointerType spt)
            {
                if (desired is PointerType)
                    return LLVM.BuildBitCast(_builder, val, _convertType(desired), "cast_tmp");
                else if (desired is SimpleType)
                    return LLVM.BuildPtrToInt(_builder, val, _convertType(desired), "cast_tmp");
            }
            else if (start is AnyType)
            {
                var anyValueElemPtr = LLVM.BuildStructGEP(_builder, val, 0, "any_val_elem_ptr_tmp");
                var anyValuePtr = LLVM.BuildLoad(_builder, anyValueElemPtr, "any_val_ptr_tmp");

                if (_isReferenceType(desired) || desired is PointerType)
                    return LLVM.BuildBitCast(_builder, anyValuePtr, _convertType(desired), "cast_tmp");
                else
                {
                    var castPtr = LLVM.BuildBitCast(_builder, anyValuePtr, LLVM.PointerType(_convertType(desired), 0), "cast_ptr_tmp");

                    return LLVM.BuildLoad(_builder, castPtr, "cast_tmp");
                }
            }
            else if (start is NullType nt)
                return _cast(val, nt.EvaluatedType, desired);
            else if (start is InterfaceType)
            {
                var thisPtr = LLVM.BuildLoad(_builder, LLVM.BuildStructGEP(_builder, val, 0, "this_elem_ptr"), "this_ptr");

                if (_isReferenceType(desired) || desired is PointerType)
                    return LLVM.BuildBitCast(_builder, thisPtr, _convertType(desired), "cast_tmp");
                else
                {
                    var castPtr = LLVM.BuildBitCast(_builder, thisPtr, LLVM.PointerType(_convertType(desired), 0), "cast_ptr_tmp");
                    return LLVM.BuildLoad(_builder, castPtr, "cast_tmp");
                }
            }
            else if (start is CustomInstance ci)
            {
                if (desired is CustomNewType)
                    return val;
                // assume we are casting a custom alias to its value
                else
                {
                    var aliasElemPtr = LLVM.BuildStructGEP(_builder, val, 1, "type_alias_elem_ptr_tmp");
                    var aliasPtr = LLVM.BuildLoad(_builder, aliasElemPtr, "type_alias_ptr_tmp");

                    var castAliasPtr = LLVM.BuildBitCast(_builder, aliasPtr, LLVM.PointerType(_convertType(desired), 0), "type_alias_castptr_tmp");

                    if (_isReferenceType(desired))
                        return castAliasPtr;
                    else
                        return LLVM.BuildLoad(_builder, castAliasPtr, "type_alias_cast_value_tmp");
                }
            }
            else if (start is TupleType stt)
            {
                // tuples can only be cast from tuples to tuples (excluding interface case which already handled)
                var dtt = (TupleType)desired;

                var castTuple = _alloca(dtt, "cast_tuple_tmp");
                for (int i = 0; i < stt.Types.Count; i++)
                {
                    var tupleElem = _getLLVMStructMember(val, i, stt.Types[i], $"tuple_elem.{i}");
                    var castTupleElem = _cast(tupleElem, stt.Types[i], dtt.Types[i]);

                    _setLLVMStructMember(castTuple, castTupleElem, i, dtt.Types[i], $"cast_tuple_elem.{i}");
                }

                return castTuple;
            }

            return _ignoreValueRef();
        }

        private ulong _getTypeCVal(DataType dt)
        {
            if (_cValLookupTable.ContainsValue(dt))
                return _cValLookupTable.Single(x => dt.Equals(x.Value)).Key;

            ulong cVal = (ulong)_cValLookupTable.Count;
            _cValLookupTable[cVal] = dt;

            return cVal;
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

        private bool _isReferenceType(DataType dt)
        {
            if (dt is CustomInstance ci)
                return ci.Parent.IsReferenceType();
            else if (dt is FunctionType ft)
                return ft.IsBoxed;
            else if (dt is SimpleType si)
                return si.Type == SimpleType.SimpleClassifier.STRING;

            return new[] { TypeClassifier.ANY, TypeClassifier.ARRAY, TypeClassifier.TUPLE,
                TypeClassifier.LIST, TypeClassifier.DICT, TypeClassifier.INTERFACE_INSTANCE,
                TypeClassifier.STRUCT_INSTANCE }.Contains(dt.Classify());
        }

        private string _getLLVMStructName(LLVMTypeRef tr)
        {
            if (tr.TypeKind != LLVMTypeKind.LLVMStructTypeKind)
                throw new NotImplementedException("Unable to get struct name of something that is not a struct kind.");

            return tr.PrintTypeToString().Split("=")[0].Trim().Substring(1);
        }       
    }
}
