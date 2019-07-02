using System.Collections.Generic;
using System.Linq;

using Whirlwind.Semantic;
using static Whirlwind.Semantic.Checker.Checker;

namespace Whirlwind.Types
{
    // declared parameters
    struct Parameter
    {
        public string Name;
        public DataType DataType;
        public bool Optional;
        public bool Indefinite;
        public bool Volatile;
        public ITypeNode DefaultValue;

        public Parameter(string name, DataType dt, bool optional, bool indefinite, bool isVol)
        {
            Name = name;
            DataType = dt;
            Optional = false;
            Indefinite = indefinite;
            Volatile = isVol;
            DefaultValue = new ValueNode("", dt);
        }

        public Parameter(string name, DataType dt, bool optional, bool indefinite, bool isVol, ITypeNode defaultVal)
        {
            Name = name;
            DataType = dt;
            Optional = true;
            Indefinite = indefinite;
            Volatile = isVol;
            DefaultValue = defaultVal;
        }

        public bool Compare(Parameter other)
        {
            if (!DataType.Equals(other.DataType))
                return false;
            if (Indefinite != other.Indefinite || Optional && !other.Optional)
                return false;
            return true;
        }

        public bool Equals(Parameter other)
        {
            // default values not compared for exact equality (for practicality's sake)
            return Name == other.Name &&
                DataType.Equals(other.DataType) &&
                Optional == other.Optional &&
                Indefinite == other.Indefinite;
        }
    }

    class FunctionType : DataType
    {
        public readonly DataType ReturnType;
        public readonly List<Parameter> Parameters;
        public readonly bool Async;

        public FunctionType(List<Parameter> parameters, DataType returnType, bool async)
        {
            Parameters = parameters;
            ReturnType = async ? MirrorType.Future(returnType) : returnType;
            Async = async;
        }

        public override TypeClassifier Classify() => TypeClassifier.FUNCTION;

        protected sealed override bool _coerce(DataType other)
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
            else if (other.Classify() == TypeClassifier.FUNCTION_GROUP)
                return ((FunctionGroup)other).Functions.Any(x => _coerce(x));

            return false;
        }

        public bool MatchArguments(ArgumentList arguments)
        {
            var data = CheckArguments(this, arguments);

            return !data.IsError;
        }

        protected override bool _equals(DataType other)
        {
            if (other.Classify() == TypeClassifier.FUNCTION)
            {
                FunctionType otherFn = (FunctionType)other;

                return Async == otherFn.Async && ReturnType.Equals(otherFn.ReturnType) && 
                    Parameters.Count == otherFn.Parameters.Count && Enumerable.Range(0, Parameters.Count).All(i => Parameters[i].Equals(otherFn.Parameters[i]));
            }

            return false;
        }

        public override DataType ConstCopy()
            => new FunctionType(Parameters, ReturnType, Async) { Constant = true };
    }

    class FunctionGroup : DataType
    {
        public readonly List<FunctionType> Functions;

        public FunctionGroup()
        {
            Functions = new List<FunctionType>();
        }

        public FunctionGroup(List<FunctionType> functions)
        {
            Functions = functions;
        }

        public bool AddFunction(FunctionType ft)
        {
            foreach (var item in Functions)
            {
                if (!CanDistinguish(ft, item))
                    return false;
            }

            Functions.Add(ft);
            return true;
        }

        public bool GetFunction(ArgumentList args, out FunctionType ft)
        {
            var fns = Functions.Where(x => x.MatchArguments(args));

            if (fns.Count() > 0)
            {
                ft = fns.First();
                return true;
            }

            ft = null;
            return false;
        }

        public static bool CanDistinguish(FunctionType a, FunctionType b)
        {
            using (var e1 = a.Parameters.GetEnumerator())
            using (var e2 = b.Parameters.GetEnumerator())
            {
                while (e1.MoveNext() && e2.MoveNext())
                {
                    if ((e1.Current.Optional || e1.Current.Indefinite) && (e2.Current.Optional || e2.Current.Indefinite))
                        return false;
                    // no need for indefinite check since void type only occurs on indefinite arguments
                    else if (e1.Current.DataType is VoidType || e2.Current.DataType is VoidType)
                        return false;
                    else if (!e1.Current.DataType.Equals(e2.Current.DataType))
                        return true;
                }
            }

            int aParamCount = a.Parameters.Count, bParamCount = b.Parameters.Count;

            if (aParamCount < bParamCount)
                return !b.Parameters[aParamCount].Optional && !b.Parameters[aParamCount].Indefinite;
            else if (aParamCount > bParamCount)
                return !a.Parameters[bParamCount].Optional && !a.Parameters[bParamCount].Indefinite;

            return false;
        }

        public override TypeClassifier Classify()
            => TypeClassifier.FUNCTION_GROUP;

        protected override bool _equals(DataType other)
        {
            if (other is FunctionGroup)
            {
                var ofg = (FunctionGroup)other;

                if (Functions.Count == ofg.Functions.Count)
                    return Functions.All(x => ofg.Functions.Contains(x));
            }

            return false;
        }

        public override DataType ConstCopy()
            => new FunctionGroup(Functions) { Constant = true };
    }
}
