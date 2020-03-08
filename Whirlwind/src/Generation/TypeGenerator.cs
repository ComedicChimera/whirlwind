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
            {
                if (pt.DataType is AnyType)
                    return LLVM.PointerType(LLVM.Int8Type(), 0);

                return LLVM.PointerType(_convertType(pt.DataType), 0);
            }
            else if (dt is StructType st)
            {
                string lName = _getLookupName(st.Name);

                Symbol symbol = null;
                foreach (var item in lName.Split("::"))
                    _table.Lookup(item, out symbol);

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

                Symbol symbol = null;
                foreach (var item in lName.Split("::"))
                    _table.Lookup(item, out symbol);

                if (symbol.DataType is InterfaceType)
                    return _getGlobalStruct(lName, usePtrTypes);
                // only other option is generic type
                else
                    return _processGeneric((GenericType)symbol.DataType, dt, usePtrTypes);
            }
            else if (dt is TupleType tt)
            {
                var tStruct = LLVM.StructType(tt.Types.Select(x => _convertType(x)).ToArray(), true);

                return usePtrTypes ? LLVM.PointerType(tStruct, 0) : tStruct;
            }
            else if (dt is FunctionType ft)
            {
                var parameters = ft.Parameters.Select(x => _convertType(x.DataType)).ToList();
                LLVMTypeRef rtType;

                if (_isReferenceType(ft.ReturnType))
                {
                    parameters.Insert(0, _convertType(ft.ReturnType, true));
                    rtType = LLVM.VoidType();
                }
                else
                    rtType = _convertType(ft.ReturnType);

                if (ft.IsBoxed)
                    parameters.Insert(0, LLVM.PointerType(LLVM.Int8Type(), 0));

                var fp = LLVM.PointerType(LLVM.FunctionType(rtType, parameters.ToArray(),
                    ft.Parameters.Count > 0 && ft.Parameters.Last().Indefinite), 0);

                return LLVM.StructType(new[] { fp, LLVM.PointerType(LLVM.Int8Type(), 0) }, false);
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
                        return cnt.Values.Count == 0 ? LLVM.Int16Type() : _getGlobalStruct(_getLookupName(parent.Name), usePtrTypes);
                }
                else if (parent.Instances.All(x => x is CustomNewType cnt && cnt.Values.Count == 0))
                    return LLVM.Int16Type();
                else
                    return _getGlobalStruct(_getLookupName(parent.Name), usePtrTypes);
            }
            else if (dt is AnyType)
                return LLVM.PointerType(LLVM.Int8Type(), 0);
            
            return LLVM.VoidType();
        }

        private LLVMTypeRef _getGlobalStruct(string name, bool usePtrTypes)
        {
            var gStruct = _globalStructs[name];

            if (usePtrTypes)
                return LLVM.PointerType(gStruct, 0);

            return gStruct;
        }

        // allow for creation of artificial struct generates
        private LLVMTypeRef _makeGenerate(GenericType gt, bool usePtrTypes, params DataType[] typeArguments)
        {
            // assume this will work :D
            gt.CreateGeneric(typeArguments.ToList(), out DataType generateType);
            var generate = gt.Generates.Single(x => x.Type.Equals(generateType));

            _genericSuffix = ".variant." + string.Join("_", generate.GenericAliases
                     .Values.Select(x => x.LLVMName()));

            _generateStruct(generate.Block, false, false);

            var gVar = _getGlobalStruct(_getLookupName(gt) + _genericSuffix, usePtrTypes);
            _genericSuffix = "";

            return gVar;
        }

        private LLVMTypeRef _processGeneric(GenericType gt, DataType ot, bool usePtrTypes)
        {
            foreach (var generate in gt.Generates)
            {
                if (generate.Type.Equals(ot))
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
            // interfaces introduce different casting rules
            if (desired is InterfaceType dInterf)
            {
                var interf = LLVM.BuildAlloca(_builder, _convertType(dInterf), "interf_box_tmp");
                var thisElemPtr = LLVM.BuildStructGEP(_builder, interf, 0, "this_elem_ptr_tmp");

                LLVMValueRef thisPtr;
                if (_isReferenceType(start) || start is PointerType)
                    thisPtr = LLVM.BuildBitCast(_builder, val, LLVM.PointerType(LLVM.Int8Type(), 0), "this_ptr_tmp");
                else
                {
                    var castPtr = LLVM.BuildAlloca(_builder, LLVM.PointerType(_convertType(start), 0), "cast_ptr_tmp");
                    LLVM.BuildStore(_builder, val, castPtr);

                    thisPtr = LLVM.BuildBitCast(_builder, castPtr, LLVM.PointerType(LLVM.Int8Type(), 0), "this_ptr_tmp");
                }

                LLVM.BuildStore(_builder, thisPtr, thisElemPtr);

                var vtableElemPtr = LLVM.BuildStructGEP(_builder, interf, 1, "vtable_elem_ptr_tmp");
                LLVM.BuildStore(_builder, _createVtable(start.GetInterface(), dInterf), vtableElemPtr);

                var cVarElemPtr = LLVM.BuildStructGEP(_builder, interf, 2, "c_var_elem_ptr_tmp");
                LLVM.BuildStore(_builder, LLVM.ConstInt(LLVM.Int16Type(), _getInterfCVar(start), new LLVMBool(0)), cVarElemPtr);

                return interf;
            }
            else if (desired is AnyType)
            {
                if (_isReferenceType(start) || start is PointerType)
                    return LLVM.BuildBitCast(_builder, val, LLVM.PointerType(LLVM.Int8Type(), 0), "cast_tmp");
                else
                {
                    var castPtr = LLVM.BuildAlloca(_builder, LLVM.PointerType(_convertType(start), 0), "cast_ptr_tmp");
                    LLVM.BuildStore(_builder, val, castPtr);

                    return LLVM.BuildBitCast(_builder, castPtr, LLVM.PointerType(LLVM.Int8Type(), 0), "cast_tmp");
                }
            }

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
                if (_isReferenceType(desired) || desired is PointerType)
                    return LLVM.BuildBitCast(_builder, val, _convertType(desired), "cast_tmp");
                else
                {
                    var castPtr = LLVM.BuildBitCast(_builder, val, LLVM.PointerType(_convertType(desired), 0), "cast_ptr_tmp");

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
            }

            return _ignoreValueRef();
        }

        private LLVMValueRef _createVtable(InterfaceType child, InterfaceType parent, string genericSuffix="")
        {
            var methods = new List<LLVMValueRef>();

            void addMethod(InterfaceType it, string methodName)
            {
                it.GetFunction(methodName, out Symbol sym);
                string methodPrefix = it.Name + genericSuffix + ".interf.";

                switch (sym.DataType.Classify())
                {
                    case TypeClassifier.FUNCTION:
                        methods.Add(_loadGlobalValue(methodPrefix + sym.Name));
                        break;
                    case TypeClassifier.FUNCTION_GROUP:
                        methods.Concat(
                            ((FunctionGroup)sym.DataType).Functions
                            .Select(x =>
                                _loadGlobalValue(methodPrefix + sym.Name + "." + 
                                string.Join(",", x.Parameters.Select(y => y.DataType.LLVMName()))
                            ))
                        );
                        break;
                    case TypeClassifier.GENERIC:
                        methods.Concat(
                            ((GenericType)sym.DataType).Generates
                            .Select(x => 
                                _loadGlobalValue(methodPrefix + sym.Name + ".variant." +
                                string.Join(",", x.GenericAliases.Select(y => y.Value.LLVMName()))
                            ))
                        );
                        break;
                    case TypeClassifier.GENERIC_GROUP:
                        // select every generate of every generic function in the generic group
                        methods.Concat(
                            ((GenericGroup)sym.DataType).GenericFunctions
                            .SelectMany(x => x.Generates.Select(y => 
                                _loadGlobalValue(methodPrefix + sym.Name + ".variant." +
                                string.Join(",", y.GenericAliases.Select(z => z.Value.LLVMName()))
                                )
                            ))
                        );
                        break;
                }
            }

            foreach (var method in parent.Methods)
            {
                if (child.Methods[method.Key] == MethodStatus.VIRTUAL)
                    addMethod(parent, method.Key.Name);
                else
                    addMethod(child, method.Key.Name);
            }

            var vtableType = _getGlobalStruct(parent.Name + genericSuffix + ".__vtable", false);
            var vtablePtr = LLVM.BuildAlloca(_builder, vtableType, "vtable_ptr_tmp");

            LLVM.BuildStore(_builder, LLVM.ConstNamedStruct(vtableType, methods.ToArray()), vtablePtr);

            return vtablePtr;
        }

        private ulong _getInterfCVar(DataType dt)
            => (ulong)dt.ToString().GetHashCode();

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
            {
                if (dt is CustomNewType cnt && cnt.Values.Count > 0)
                    return true;

                if (ci.Parent.Instances.Count > 1 && !ci.Parent.Instances.All(x => x is CustomNewType))
                    return true;
            }
            else if (dt is FunctionType ft)
                return ft.IsBoxed;

            return new[] { TypeClassifier.ANY, TypeClassifier.ARRAY, TypeClassifier.TUPLE,
                TypeClassifier.LIST, TypeClassifier.DICT, TypeClassifier.INTERFACE_INSTANCE,
                TypeClassifier.STRUCT_INSTANCE }.Contains(dt.Classify());
        }
    }
}
