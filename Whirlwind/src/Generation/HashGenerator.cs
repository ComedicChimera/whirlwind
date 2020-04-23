using System;
using System.Linq;

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
                    /*case 2:
                        // hashedValue = _callHashFunction(vref, "string_hash");
                        break;*/
                    // should never happen (integers should never be given hash directly)
                    default:
                        throw new NotImplementedException();
                }
            }
            else if (dt is PointerType pt)
                hashedValue = LLVM.BuildPtrToInt(_builder, vref, LLVM.Int64Type(), "pointer_hash_tmp");
            /*else if (dt is TupleType tt)
            {

            }*/
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
