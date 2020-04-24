using System;
using System.Linq;
using System.Collections.Generic;

using LLVMSharp;

using Whirlwind.Semantic;
using Whirlwind.Types;

namespace Whirlwind.Generation
{
    partial class Generator
    {
        private Tuple<LLVMValueRef, bool> _getHash(LLVMValueRef vref, DataType dt)
        {
            LLVMValueRef hashedValue;
            if (dt is SimpleType st)
            {
                switch (_getSimpleClass(st))
                {
                    case 1:
                        hashedValue = LLVM.BuildBitCast(_builder, vref, LLVM.Int64Type(), "float_hash_tmp");
                        break;
                    case 2:
                        if (_globalScope.ContainsKey("string_hash"))
                            hashedValue = LLVM.BuildCall(_builder, _globalScope["string_hash"].Vref, new[] { vref }, "string_hash_tmp");
                        else
                            throw new GeneratorException("Missing necessary implementation of string hash function.");
                        break;
                    // should never happen (integers should never be given hash directly)
                    default:
                        throw new NotImplementedException();
                }
            }
            else if (dt is PointerType pt)
                hashedValue = LLVM.BuildPtrToInt(_builder, vref, LLVM.Int64Type(), "pointer_hash_tmp");
            /*else if (dt is TupleType tt)
            {
                if (_symTable.ContainsKey("tuple_hash"))
                {
                    var tupleHashGenerate = _makeGenerateFunction((GenericType)_symTable["tuple_hash"].DataType, 
                        new List<DataType> { )
                }
                else
                    throw new GeneratorException("Missing necessary implementation of tuple hash function.");
            }*/
            // if a custom alias reaches this far than it can be assumed to be a raw vref
            else if (dt is CustomAlias ca)
                return _getHash(vref, ca.Type);
            else
                hashedValue = _callMethod(vref, dt, "__hash__", new SimpleType(SimpleType.SimpleClassifier.LONG));

            return new Tuple<LLVMValueRef, bool>(hashedValue, hashedValue.IsConstant());
        }

        private bool _needsHash(DataType dt)
        {
            if (dt is SimpleType st && _getSimpleClass(st) == 0)
                return false;
            else if (dt is CustomInstance ci)
            {
                if (ci.Parent.IsReferenceType())
                    throw new NotImplementedException();

                // no reference types => no values => i16
                if (ci.Parent.Instances.All(x => x is CustomNewType))
                    return false;

                // can assume it is a pure type alias if it reaches this point
                return _needsHash(((CustomAlias)ci.Parent.Instances.First()).Type);
            }

            return true;
        }
    }
}
