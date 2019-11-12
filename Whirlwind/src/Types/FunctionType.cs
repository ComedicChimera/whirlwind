using System.Collections.Generic;
using System.Linq;

using Whirlwind.Semantic;

using static Whirlwind.Semantic.Checker.Checker;

namespace Whirlwind.Types
{
    // declared parameters
    class Parameter
    {
        public string Name;
        public DataType DataType;
        public bool Optional;
        public bool Indefinite;
        public bool Volatile;
        public bool Owned;
        public ITypeNode DefaultValue;

        public Parameter(string name, DataType dt, bool optional, bool indefinite, bool isVol, bool isOwned)
        {
            Name = name;
            DataType = dt;
            Optional = false;
            Indefinite = indefinite;
            Volatile = isVol;
            Owned = isOwned;
            DefaultValue = new ValueNode("", dt);
        }

        public Parameter(string name, DataType dt, bool optional, bool indefinite, bool isVol, bool isOwned, ITypeNode defaultVal)
        {
            Name = name;
            DataType = dt;
            Optional = true;
            Indefinite = indefinite;
            Volatile = isVol;
            Owned = isOwned;
            DefaultValue = defaultVal;
        }

        public bool Compare(Parameter other)
        {
            if (!DataType.Coerce(other.DataType))
                return false;
            if (Indefinite != other.Indefinite || Optional && !other.Optional || Owned != other.Owned)
                return false;
            return true;
        }

        public bool Equals(Parameter other)
        {
            // default values not compared for exact equality (for practicality's sake)
            return Name == other.Name &&
                DataType.Equals(other.DataType) &&
                Optional == other.Optional &&
                Indefinite == other.Indefinite && 
                Owned == other.Owned;
        }

        public override bool Equals(object obj)
        {
            if (obj is Parameter p && obj != null)
                return Equals(p);

            return base.Equals(obj);
        }
    }

    class FunctionType : DataType
    {
        public DataType ReturnType;
        public readonly bool Async;
        public readonly bool Mutable;

        public List<Parameter> Parameters;

        public FunctionType(List<Parameter> parameters, DataType returnType, bool async)
        {
            Parameters = parameters;
            ReturnType = returnType;
            Async = async;
        }

        public FunctionType(List<Parameter> parameters, DataType returnType, bool async, bool mutable)
        {
            Parameters = parameters;
            ReturnType = returnType;
            Async = async;
            Mutable = mutable;
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
            if (other is FunctionType otherFn)
                return Async == otherFn.Async && ReturnType.Equals(otherFn.ReturnType) && Parameters.EnumerableEquals(otherFn.Parameters);

            return false;
        }

        public override DataType ConstCopy()
            => new FunctionType(Parameters, ReturnType, Async, Mutable) { Constant = true };

        public FunctionType NonConstCopy()
            => new FunctionType(Parameters, ReturnType, Async) { Constant = false };

        public override string ToString()
        {
            string baseString = Async ? "async" : "func";
          
            var stringParams = new List<string>();

            foreach (var param in Parameters)
            {
                string stringParam = "";

                if (param.Indefinite)
                    stringParam += "...";
                else if (param.Optional)
                    stringParam += "~";

                stringParam += param.DataType.ToString();

                stringParams.Add(stringParam);
            }

            return baseString += $"({string.Join(", ", stringParams)})({(ReturnType is NoneType ? "" : ReturnType.ToString())})";
        }
    }

    class FunctionGroup : DataType
    {
        public readonly string Name;
        public List<FunctionType> Functions;

        public FunctionGroup(string name)
        {
            Name = name;
            Functions = new List<FunctionType>();
        }

        public FunctionGroup(string name, List<FunctionType> functions)
        {
            Name = name;
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
                    else if (e1.Current.DataType is NoneType || e2.Current.DataType is NoneType)
                        return false;
                    else if (!e1.Current.DataType.Equals(e2.Current.DataType) 
                        && !(e1.Current.DataType is GenericPlaceholder || e2.Current.DataType is GenericPlaceholder))
                    {
                        return true;
                    }                     
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
            if (other is FunctionGroup ofg)
                return Functions.EnumerableEquals(ofg.Functions);

            return false;
        }

        public override DataType ConstCopy()
            => new FunctionGroup(Name, Functions) { Constant = true };

        public override string ToString() => $"FunctionGroup[{Name}]";
    }
}
