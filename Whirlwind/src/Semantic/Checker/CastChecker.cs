using Whirlwind.Types;

using System.Linq;
using System.Collections.Generic;

namespace Whirlwind.Semantic.Checker
{
    static partial class Checker
    {
        public static bool TypeCast(DataType start, DataType desired)
        {
            if (desired.Coerce(start))
                return true;

            if (!desired.Constant && start.Constant)
                return false;

            switch (start.Classify())
            {
                case TypeClassifier.SIMPLE:
                    {
                        if (desired.Classify() == TypeClassifier.SIMPLE)
                        {
                            switch (((SimpleType)start).Type)
                            {
                                case SimpleType.SimpleClassifier.BOOL:
                                    return ((SimpleType)desired).Type != SimpleType.SimpleClassifier.STRING;
                                case SimpleType.SimpleClassifier.BYTE:
                                case SimpleType.SimpleClassifier.CHAR:
                                    return ((SimpleType)desired).Type != SimpleType.SimpleClassifier.BOOL;
                            }

                            // possibly add extra guard logic here
                            if (Numeric(start) && Numeric(desired))
                                return true;
                        }
                        else if (desired.Classify() == TypeClassifier.POINTER)
                            return ((SimpleType)start).Type == SimpleType.SimpleClassifier.INTEGER && ((SimpleType)start).Unsigned;
                    }
                    break;
                case TypeClassifier.ARRAY:
                case TypeClassifier.LIST:
                    if (start.Classify() == TypeClassifier.ARRAY && desired.Classify() == TypeClassifier.POINTER)
                        return ((ArrayType)start).ElementType.Equals(((PointerType)desired).Type);
                    else if (new[] { TypeClassifier.ARRAY, TypeClassifier.LIST }.Contains(desired.Classify()))
                        return TypeCast((start as IIterable).GetIterator(), (desired as IIterable).GetIterator());
                    else if (desired.Classify() == TypeClassifier.POINTER)
                        return TypeCast((start as IIterable).GetIterator(), ((PointerType)desired).Type);
                    break;
                case TypeClassifier.DICT:
                    if (desired.Classify() == TypeClassifier.DICT)
                        return TypeCast(((DictType)start).KeyType, ((DictType)desired).KeyType) && TypeCast(((DictType)start).ValueType, ((DictType)desired).ValueType);
                    break;
                case TypeClassifier.POINTER:
                    if (desired.Classify() == TypeClassifier.POINTER)
                    {
                        PointerType startPointer = (PointerType)start, desiredPointer = (PointerType)desired;
                        return TypeCast(startPointer.Type, desiredPointer.Type) && startPointer.Pointers == desiredPointer.Pointers;
                    }
                    else if (desired.Classify() == TypeClassifier.SIMPLE)
                        return ((SimpleType)desired).Type == SimpleType.SimpleClassifier.INTEGER && ((SimpleType)desired).Unsigned;
                    else if (desired.Classify() == TypeClassifier.ARRAY)
                        return ((ArrayType)desired).ElementType.Equals(((PointerType)start).Type);
                    break;
                case TypeClassifier.FUNCTION:
                    if (desired.Classify() == TypeClassifier.FUNCTION)
                    {
                        FunctionType startFn = (FunctionType)start, desiredFn = (FunctionType)desired;

                        if (startFn.Parameters.Count != desiredFn.Parameters.Count)
                        {
                            List<Parameter>
                                startMandatoryParams = startFn.Parameters.Where(x => !x.Optional && !x.Indefinite).ToList(),
                                desiredMandatoryParams = desiredFn.Parameters.Where(x => !x.Optional && !x.Indefinite).ToList();

                            if (startMandatoryParams.Count != desiredMandatoryParams.Count)
                                return false;

                            using (var e1 = startMandatoryParams.GetEnumerator())
                            using (var e2 = desiredMandatoryParams.GetEnumerator())
                            {
                                while (e1.MoveNext() && e2.MoveNext())
                                {
                                    if (!e2.Current.DataType.Coerce(e1.Current.DataType))
                                        return false;
                                }
                            }
                        }
                        else
                        {
                            using (var e1 = startFn.Parameters.GetEnumerator())
                            using (var e2 = desiredFn.Parameters.GetEnumerator())
                            {
                                while (e1.MoveNext() && e2.MoveNext())
                                {
                                    if (!e2.Current.DataType.Coerce(e1.Current.DataType))
                                        return false;
                                }
                            }
                        }

                        return desiredFn.ReturnType.Coerce(startFn.ReturnType);
                    }
                    break;
                case TypeClassifier.STRUCT_INSTANCE:
                    if (desired.Classify() == TypeClassifier.STRUCT)
                    {
                        return ((StructType)start).Members == ((StructType)desired).Members;
                    }
                    break;
                case TypeClassifier.INTERFACE_INSTANCE:
                    {
                        var startInstance = (InterfaceType)start;

                        if (startInstance.Coerce(desired.GetInterface()))
                            return true;
                    }
                    break;
            }

            if (start.Classify() == TypeClassifier.TUPLE && desired.Classify() == TypeClassifier.TUPLE)
            {
                TupleType startTuple = (TupleType)start,
                    desiredTuple = (TupleType)desired;

                if (startTuple.Types.Count == desiredTuple.Types.Count)
                    return Enumerable.Range(0, startTuple.Types.Count).All(i => TypeCast(startTuple.Types[i], desiredTuple.Types[i]));
            }

            return false;
        }
    }
}
