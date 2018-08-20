using Whirlwind.Types;

using System.Linq;
using System.Collections.Generic;

namespace Whirlwind.Semantic.Checker
{
    static partial class Checker
    {
        public static bool TypeCast(IDataType start, IDataType desired)
        {
            if (desired.Coerce(start))
                return true;

            switch (start.Classify())
            {
                case "SIMPLE_TYPE":
                    {
                        if (desired.Classify() == "SIMPLE_TYPE")
                        {
                            switch (((SimpleType)start).Type)
                            {
                                case SimpleType.DataType.BOOL:
                                    return ((SimpleType)desired).Type != SimpleType.DataType.STRING;
                                case SimpleType.DataType.BYTE:
                                case SimpleType.DataType.CHAR:
                                    return ((SimpleType)desired).Type != SimpleType.DataType.BOOL;
                            }

                            // possibly add extra guard logic here
                            if (Numeric(start) && Numeric(desired))
                                return true;
                        }
                        else if (desired.Classify() == "POINTER")
                            return ((SimpleType)start).Type == SimpleType.DataType.INTEGER && ((SimpleType)start).Unsigned;
                    }
                    break;
                case "ARRAY":
                case "LIST":
                    if (new[] { "ARRAY", "LIST" }.Contains(desired.Classify()))
                        return TypeCast((start as ArrayType).ElementType, (desired as ArrayType).ElementType);
                    break;
                case "MAP":
                    if (desired.Classify() == "MAP")
                        return TypeCast(((MapType)start).KeyType, ((MapType)desired).KeyType) && TypeCast(((MapType)start).ValueType, ((MapType)desired).ValueType);
                    break;
                case "POINTER":
                    if (desired.Classify() == "POINTER")
                    {
                        PointerType startPointer = (PointerType)start, desiredPointer = (PointerType)desired;
                        return TypeCast(startPointer.Type, desiredPointer.Type) && startPointer.Pointers == desiredPointer.Pointers;
                    }
                    else if (desired.Classify() == "SIMPLE_TYPE")
                        return ((SimpleType)desired).Type == SimpleType.DataType.INTEGER && ((SimpleType)desired).Unsigned;
                    break;
                case "FUNCTION":
                    if (desired.Classify() == "FUNCTION")
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
                case "STRUCT_INSTANCE":
                    if (desired.Classify() == "STRUCT")
                    {
                        return ((StructType)start).Members == ((StructType)desired).Members;
                    }
                    break;
                case "MODULE_INSTANCE":
                    if (desired.Classify() == "MODULE")
                    {
                        ModuleInstance startInstance = (ModuleInstance)start,
                            desiredInstance = ((ModuleType)desired).GetInstance();

                        if (desiredInstance.Inherits == startInstance.Inherits)
                        {
                            if (startInstance.ListProperties() != desiredInstance.ListProperties())
                                return false;

                            using (var e1 = startInstance.ListProperties().GetEnumerator())
                            using (var e2 = desiredInstance.ListProperties().GetEnumerator())
                            {
                                while (e1.MoveNext() && e2.MoveNext())
                                {
                                    if (startInstance.GetProperty(e2.Current, out Symbol symbol))
                                    {
                                        desiredInstance.GetProperty(e2.Current, out Symbol checkSymbol);

                                        if (checkSymbol != symbol)
                                            return false;
                                    }
                                    else return false;
                                }
                            }
                        }

                    }
                    break;
                case "INTERFACE_INSTANCE":
                    if (desired.Classify() == "MODULE")
                    {
                        var startInstance = (InterfaceType)start;
                        var desiredInstance = (ModuleType)desired;

                        if (startInstance.MatchModule(desiredInstance))
                            return true;
                    }
                    break;
            }

            if (start.Classify().StartsWith("TUPLE") && desired.Classify().StartsWith("TUPLE"))
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
