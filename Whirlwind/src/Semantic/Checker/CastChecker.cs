using System.Linq;
using System.Collections.Generic;

using Whirlwind.Types;

namespace Whirlwind.Semantic.Checker
{
    static partial class Checker
    {
        public static bool TypeCast(DataType start, DataType desired)
        {
            if (desired.Coerce(start))
                return true;

            if (start.Constant != desired.Constant && desired.ConstCopy().Coerce(start.ConstCopy()))
                return true;

            // although alias possesses a type, for the purposes of casting, the alias is actually variadic
            // any type can cast to anything
            if (start is GenericAlias || start is AnyType)
                return true;

            // any pointers can be cast to anything (b/c they are basically memory addresses)
            // but, when casting a any pointer to a non-pointer, the pointer is implicitly dereferenced
            // in almost all cases (excluding function types)
            if (start is PointerType apt && apt.DataType.Classify() == TypeClassifier.ANY && !apt.IsDynamicPointer)
                return true;

            if (desired.Classify() == TypeClassifier.INTERFACE && !(start is InterfaceType))
            {
                InterfaceType startInterf = start.GetInterface(), desiredInterf = (InterfaceType)desired;

                if (startInterf.Implements.Any(x => x.Equals(desired)))
                    return true;
                else if (desiredInterf.Coerce(startInterf))
                    return true;
                else if (desiredInterf.Methods.All(x => startInterf.Methods.Any(y => x.Key.Equals(y.Key))))
                    return true;
            }

            switch (start.Classify())
            {
                case TypeClassifier.SIMPLE:
                    {
                        if (desired.Classify() == TypeClassifier.SIMPLE)
                        {
                            // allow for unsigned to signed casting
                            if (((SimpleType)start).Type == ((SimpleType)desired).Type)
                                return true;

                            switch (((SimpleType)start).Type)
                            {
                                case SimpleType.SimpleClassifier.BOOL:
                                    return ((SimpleType)desired).Type != SimpleType.SimpleClassifier.STRING;
                                case SimpleType.SimpleClassifier.BYTE:
                                case SimpleType.SimpleClassifier.CHAR:
                                    return ((SimpleType)desired).Type == SimpleType.SimpleClassifier.INTEGER;
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
                        return ((ArrayType)start).ElementType.Equals(((PointerType)desired).DataType);
                    else if (new[] { TypeClassifier.ARRAY, TypeClassifier.LIST }.Contains(desired.Classify()))
                        return TypeCast((start as IIterable).GetIterator(), (desired as IIterable).GetIterator());
                    else if (desired.Classify() == TypeClassifier.POINTER)
                        return TypeCast((start as IIterable).GetIterator(), ((PointerType)desired).DataType);
                    else if (start is ArrayType at && at.ElementType is SimpleType st && st.Type == SimpleType.SimpleClassifier.BYTE)
                    {
                        if (desired is SimpleType dst)
                        {
                            if (at.Size == -1)
                                return true;

                            switch (dst.Type)
                            {
                                case SimpleType.SimpleClassifier.BOOL:
                                case SimpleType.SimpleClassifier.BYTE:
                                    return at.Size == 1;
                                case SimpleType.SimpleClassifier.SHORT:
                                    return at.Size == 2;
                                case SimpleType.SimpleClassifier.CHAR:
                                case SimpleType.SimpleClassifier.FLOAT:
                                case SimpleType.SimpleClassifier.INTEGER:
                                    return at.Size == 4;
                                case SimpleType.SimpleClassifier.LONG:
                                case SimpleType.SimpleClassifier.DOUBLE:
                                    return at.Size == 8;
                                default:
                                    return true;
                            }
                        }
                    }
                    break;
                case TypeClassifier.DICT:
                    if (desired.Classify() == TypeClassifier.DICT)
                        return TypeCast(((DictType)start).KeyType, ((DictType)desired).KeyType) && TypeCast(((DictType)start).ValueType, ((DictType)desired).ValueType);
                    break;
                case TypeClassifier.POINTER:
                    if (desired is PointerType pt)
                        return ((PointerType)start).IsDynamicPointer == pt.IsDynamicPointer && 
                            TypeCast(((PointerType)start).DataType, ((PointerType)desired).DataType);
                    else if (desired.Classify() == TypeClassifier.SIMPLE)
                        return ((SimpleType)desired).Type == SimpleType.SimpleClassifier.INTEGER && ((SimpleType)desired).Unsigned;
                    else if (desired.Classify() == TypeClassifier.ARRAY)
                        return ((ArrayType)desired).ElementType.Equals(((PointerType)start).DataType);
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
                case TypeClassifier.FUNCTION_GROUP:
                    if (desired is FunctionType && ((FunctionGroup)start).Functions.Any(x => x.Equals(desired)))
                            return true;
                    break;
                case TypeClassifier.STRUCT_INSTANCE:
                    if (desired.Classify() == TypeClassifier.STRUCT_INSTANCE)
                        return ((StructType)start).Members.EnumerableEquals(((StructType)desired).Members);
                    break;
                case TypeClassifier.INTERFACE_INSTANCE:
                    {
                        if (desired.GetInterface().Implements.Any(x => x.Equals(start)))
                            return true;
                    }
                    break;
                case TypeClassifier.TYPE_CLASS_INSTANCE:
                    {
                        var startInstance = (CustomInstance)start;

                        if (desired is CustomNewType)
                            return startInstance.Parent.Instances.Any(x => x.Equals(startInstance));
                        else
                        {
                            return startInstance.Parent.Instances.Where(x => x is CustomAlias)
                                .Select(x => (CustomAlias)x)
                                .Any(x => x.Type.Coerce(desired));
                        }
                    }
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
