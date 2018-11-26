using System.Collections.Generic;
using Whirlwind.Semantic;

namespace Whirlwind.Types
{
    // declared parameters
    struct Parameter
    {
        public string Name;
        public IDataType DataType;
        public bool Optional;
        public bool Indefinite;
        public bool Constant;
        public ITypeNode DefaultValue;

        public Parameter(string name, IDataType dt, bool indefinite, bool constant)
        {
            Name = name;
            DataType = dt;
            Optional = false;
            Indefinite = indefinite;
            Constant = constant;
            DefaultValue = new ValueNode("", DataType);
        }

        public Parameter(string name, IDataType dt, bool indefinite, bool constant, ITypeNode defaultVal)
        {
            Name = name;
            DataType = dt;
            Optional = true;
            Indefinite = indefinite;
            Constant = constant;
            DefaultValue = defaultVal;
        }

        public bool Compare(Parameter other)
        {
            if (!DataType.Coerce(other.DataType))
                return false;
            if (Indefinite != other.Indefinite)
                return false;
            return true;
        }
    }

    class FunctionType : IDataType
    {
        public readonly IDataType ReturnType;
        public readonly List<Parameter> Parameters;
        public readonly bool Async;

        public FunctionType(List<Parameter> parameters, IDataType returnType, bool async)
        {
            Parameters = parameters;
            ReturnType = async ? MirrorType.Future(returnType) : returnType;
            Async = async;
        }

        public TypeClassifier Classify() => TypeClassifier.FUNCTION;

        public bool Coerce(IDataType other)
        {
            if (other.Classify() == TypeClassifier.FUNCTION)
            {
                var otherFunction = ((FunctionType)other);
                if (otherFunction.Async != Async)
                    return false;
                if (Parameters.Count != otherFunction.Parameters.Count)
                    return false;
                for (int i = 0; i < Parameters.Count; i++)
                {
                    if (!Parameters[i].Compare(otherFunction.Parameters[i]))
                        return false;
                }
                if (ReturnType.Coerce(((FunctionType)other).ReturnType))
                    return true;
                return false;
            }
            return false;
        }

        public bool MatchParameters(List<IDataType> parameters)
        {
            return false;
        }
    }
}
