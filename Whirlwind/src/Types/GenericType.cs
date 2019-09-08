using System.Collections.Generic;
using System.Linq;

using Whirlwind.Semantic;

namespace Whirlwind.Types
{
    // represents the generic placeholder in the generic signature
    class GenericPlaceholder : DataType
    {
        public readonly string Name;

        public GenericPlaceholder(string name)
        {
            Name = name;
        }

        // the actual type of placeholder is irrelevant
        public override bool Coerce(DataType other) => true;
        public override TypeClassifier Classify() => TypeClassifier.GENERIC_PLACEHOLDER;

        protected override bool _equals(DataType other)
        {
            if (other.Classify() == TypeClassifier.GENERIC_PLACEHOLDER)
                return Name == ((GenericPlaceholder)other).Name;

            return false;
        }

        public override DataType ConstCopy()
            => new GenericPlaceholder(Name); // implicit const

        public override string ToString() => Name;
    }

    // represents the various aliases of the generics (ie. the T in id<T>)
    class GenericAlias : DataType
    {
        public readonly DataType ReplacementType;

        public GenericAlias(DataType replacementType)
        {
            ReplacementType = replacementType;
            Constant = true;
        }

        public override bool Coerce(DataType other) => false;
        public override TypeClassifier Classify() => TypeClassifier.GENERIC_ALIAS;

        protected override bool _equals(DataType other) => false;

        public override DataType ConstCopy()
            => new GenericAlias(ReplacementType); // implicit const

        public override string ToString() => ReplacementType.ToString();
    }

    // function used to evaluate generic body
    delegate GenericGenerate GenericEvaluator(Dictionary<string, DataType> aliases, GenericType parent);

    // a struct containing the generic name and its restrictors
    struct GenericVariable
    {
        public readonly string Name;
        public readonly List<DataType> Restrictors;

        public GenericVariable(string name, List<DataType> restrictors)
        {
            Name = name;
            Restrictors = restrictors;
        }

        public bool Equals(GenericVariable other)
        {
            return Name == other.Name &&
                Restrictors.Count == other.Restrictors.Count &&
                Restrictors.All(x => other.Restrictors.Contains(x));
        }
    }

    // represents a single generic generate instance
    struct GenericGenerate
    {
        public DataType Type;
        public Dictionary<string, DataType> GenericAliases;
        public BlockNode Block;

        public GenericGenerate(DataType dt, Dictionary<string, DataType> genAliases, BlockNode block)
        {
            Type = dt;
            GenericAliases = genAliases;
            Block = block;
        }
    }

    // represents the full generic object (entire generic method, obj, ect.)
    class GenericType : DataType
    {
        private readonly GenericEvaluator _evaluator;
        private readonly List<List<DataType>> _variants;

        public DataType DataType { get; private set; }
        public List<GenericGenerate> Generates { get; private set; }

        public readonly List<GenericVariable> GenericVariables;

        public GenericType(List<GenericVariable> generics, DataType type, GenericEvaluator evaluator)
        {
            GenericVariables = generics;
            DataType = type;

            _evaluator = evaluator;

            _variants = new List<List<DataType>>();

            Generates = new List<GenericGenerate>();

            Constant = true;
        }

        public bool CreateGeneric(List<DataType> dataTypes, out DataType genericType)
        {
            if (dataTypes.Count == GenericVariables.Count)
            {
                var aliases = new Dictionary<string, DataType>();

                using (var e1 = GenericVariables.GetEnumerator())
                using (var e2 = dataTypes.GetEnumerator())
                {
                    while (e1.MoveNext() && e2.MoveNext())
                    {
                        if (e1.Current.Restrictors.Count > 0 /* check for empty restrictors */
                            && !e1.Current.Restrictors.Any(y => y.Coerce(e2.Current)))
                        {
                            genericType = null;
                            return false;
                        }
                        else
                            aliases.Add(e1.Current.Name, e2.Current);
                    }
                }

                var generate = _evaluator(aliases, this);

                // prevent generic placeholders from creating unnecessary generates
                // and prevent duplicate generates from being creates
                if (!aliases.Any(x => x.Value is GenericPlaceholder) && 
                    !Generates.Any(x => x.GenericAliases.DictionaryEquals(generate.GenericAliases)))
                {
                    Generates.Add(generate);
                }                 

                genericType = generate.Type;
                return true;
            }


            genericType = null;
            return false;
        }

        public bool Infer(ArgumentList arguments, out List<DataType> inferredTypes)
        {
            switch (DataType.Classify())
            {
                case TypeClassifier.FUNCTION:
                    return _inferFromFunction((FunctionType)DataType, arguments, out inferredTypes);
                case TypeClassifier.STRUCT:
                    if (((StructType)DataType).GetConstructor(arguments, out FunctionType constructor))
                        return _inferFromFunction(constructor, arguments, out inferredTypes);
                    break;
            }

            inferredTypes = new List<DataType>();
            return false;
        }

        public bool Infer(Dictionary<string, DataType> members, out List<DataType> inferredTypes)
        {
            var completedAliases = new Dictionary<string, DataType>();

            // impossible for instance struct generic to exist
            if (DataType is StructType st)
            {
                foreach (var member in members)
                {
                    if (!st.Members.Keys.Contains(member.Key)
                        || !_getCompletedAliases(st.Members[member.Key].DataType, member.Value, completedAliases))
                    {
                        inferredTypes = new List<DataType>();
                        return false;
                    }
                }

                if (completedAliases.Count == GenericVariables.Count)
                {
                    if (completedAliases.All(x => {
                        var res = GenericVariables.Where(y => y.Name == x.Key).First().Restrictors;
                        return res.Count == 0 || res.Any(y => y.Coerce(x.Value));
                    }))
                    {
                        inferredTypes = GenericVariables.Select(x => completedAliases[x.Name]).ToList();
                        return true;
                    }
                }
            }

            inferredTypes = null;
            return false;
        }

        public bool InferFromType(DataType dt, out List<DataType> inferredTypes)
        {
            var completedAliases = new Dictionary<string, DataType>();

            if (!_getCompletedAliases(DataType, dt, completedAliases))
            {
                inferredTypes = null;
                return false;
            }               

            if (completedAliases.Count == GenericVariables.Count)
            {
                if (completedAliases.All(x => {
                    var res = GenericVariables.Where(y => y.Name == x.Key).First().Restrictors;
                    return res.Count == 0 || res.Any(y => y.Coerce(x.Value));
                }))
                {
                    inferredTypes = GenericVariables.Select(x => completedAliases[x.Name]).ToList();
                    return true;
                }
            }

            inferredTypes = null;
            return false;
        }

        public bool AddVariant(List<DataType> dataTypes)
        {
            if (dataTypes.Count != GenericVariables.Count)
                return false;

            if (_variants.Contains(dataTypes))
                return false;

            using (var e1 = GenericVariables.Select(x => x.Restrictors).GetEnumerator())
            using (var e2 = dataTypes.GetEnumerator())
            {
                while (e1.MoveNext() && e2.MoveNext())
                {
                    if (e1.Current.Count == 0)
                        continue;
                    if (!e1.Current.Contains(e2.Current))
                        return false;
                }
            }

            _variants.Add(dataTypes);
            return true;
        }

        public override bool Coerce(DataType other) => Equals(other);

        public override TypeClassifier Classify() => TypeClassifier.GENERIC;

        public override DataType ConstCopy()
        {
            var tt = new GenericType(GenericVariables, DataType, _evaluator)
            {
                Generates = Generates
            }; // implicit const

            foreach (var variant in _variants)
                tt._variants.Add(variant);

            return tt;
        }

        public bool CompareGenerics(List<GenericVariable> other)
        {
            return GenericVariables.Count == other.Count &&
                GenericVariables.All(x => other.Where(y => y.Equals(x)).Count() > 0);
        }

        private bool _inferFromFunction(FunctionType fnType, ArgumentList arguments, out List<DataType> inferredTypes)
        {
            var completedAliases = new Dictionary<string, DataType>();

            using (var e1 = fnType.Parameters.GetEnumerator())
            using (var e2 = arguments.UnnamedArguments.GetEnumerator())
            {
                while (e1.MoveNext() && e2.MoveNext())
                {
                    if (!_getCompletedAliases(e1.Current.DataType, e2.Current, completedAliases))
                    {
                        inferredTypes = new List<DataType>();
                        return false;
                    }
                }
            }

            foreach (var nArg in arguments.NamedArguments)
            {
                var matches = fnType.Parameters.Where(x => x.Name == nArg.Key);

                // don't throw error, because invalid names will be caught later will be caught later
                if (matches.Count() > 0)
                {
                    if (!_getCompletedAliases(matches.First().DataType, nArg.Value, completedAliases))
                    {
                        inferredTypes = new List<DataType>();
                        return false;
                    }
                }
            }

            if (completedAliases.Count == GenericVariables.Count)
            {
                GenericVariables.Where(y => y.Name == "T").First().Restrictors.Any(y => y.Coerce(completedAliases["T"]));

                if (completedAliases.All(x => {
                    var res = GenericVariables.Where(y => y.Name == x.Key).First().Restrictors;
                    return res.Count == 0 || res.Any(y => y.Coerce(x.Value));
                }))
                {
                    inferredTypes = GenericVariables.Select(x => completedAliases[x.Name]).ToList();
                    return true;
                }
            }

            inferredTypes = new List<DataType>();
            return false;
        }

        private bool _getCompletedAliases(DataType ta, DataType tb, Dictionary<string, DataType> completedAliases)
        {
            // if they can't even coerce each other, no need to check for aliases at all
            // only need to check in one direction b/c if they are coercible ta will always have looser type definition
            // for what we are looking at here
            if (!ta.Coerce(tb))
                return false;

            // no need to check for alignment when getting aliases since coercion already covers that

            if (ta.Classify() == TypeClassifier.GENERIC_PLACEHOLDER)
            {
                string placeholderName = ((GenericPlaceholder)ta).Name;

                if (completedAliases.ContainsKey(placeholderName))
                {
                    var ca = completedAliases[placeholderName];

                    if (!ca.Coerce(tb))
                    {
                        if (!tb.Coerce(ca))
                            return false;

                        completedAliases[placeholderName] = tb;
                    }

                    return true;
                }
                else
                {
                    if (tb is IncompleteType)
                        return false;

                    completedAliases[placeholderName] = tb;
                    return true;
                }
            }

            if (ta.Classify() != tb.Classify())
            {
                if (ta is IIterable ita && tb is IIterable itb && !new[] { ta.Classify(), tb.Classify() }.Contains(TypeClassifier.DICT))
                    return _getCompletedAliases(ita.GetIterator(), itb.GetIterator(), completedAliases);
                else
                    return false;
            }


            switch (ta.Classify())
            {
                case TypeClassifier.TUPLE:
                    {
                        TupleType tta = (TupleType)ta, ttb = (TupleType)tb;

                        return Enumerable.Range(0, tta.Types.Count).All(i => _getCompletedAliases(tta.Types[i], ttb.Types[i], completedAliases));
                    }
                case TypeClassifier.ARRAY:
                case TypeClassifier.LIST:
                    return _getCompletedAliases(((IIterable)ta).GetIterator(), ((IIterable)tb).GetIterator(), completedAliases);
                case TypeClassifier.DICT:
                    {
                        DictType dta = (DictType)ta, dtb = (DictType)tb;

                        return _getCompletedAliases(dta.KeyType, dtb.KeyType, completedAliases)
                            && _getCompletedAliases(dta.ValueType, dtb.ValueType, completedAliases);
                    }
                case TypeClassifier.FUNCTION:
                    {
                        FunctionType fta = (FunctionType)ta, ftb = (FunctionType)tb;

                        if (!_getCompletedAliases(fta.ReturnType, ftb.ReturnType, completedAliases))
                            return false;

                        return Enumerable.Range(0, fta.Parameters.Count)
                            .All(i => _getCompletedAliases(fta.Parameters[i].DataType, ftb.Parameters[i].DataType, completedAliases));
                    }
                case TypeClassifier.POINTER:
                    return _getCompletedAliases(((PointerType)ta).DataType, ((PointerType)tb).DataType, completedAliases);
                case TypeClassifier.STRUCT_INSTANCE:
                    {
                        StructType sta = (StructType)ta, stb = (StructType)tb;

                        return sta.Members.All(x => _getCompletedAliases(x.Value.DataType, stb.Members[x.Key].DataType, completedAliases));
                    }
                case TypeClassifier.INTERFACE_INSTANCE:
                    {
                        var methods = ((InterfaceType)ta).Methods;

                        foreach (var method in methods)
                        {
                            // coercion checking means we know it exists
                            ((InterfaceType)tb).GetFunction(method.Key.Name, out Symbol symbol);

                            if (!_getCompletedAliases(method.Key.DataType, symbol.DataType, completedAliases))
                                return false;
                        }

                        return true;
                    }
            }

            // low level type checking doesn't matter here (caught in coercion check above)
            return true;
        }       

        protected override bool _equals(DataType other)
        {
            if (other.Classify() == TypeClassifier.GENERIC)
            {
                var gt = (GenericType)other;

                if (!DataType.Equals(gt.DataType))
                    return false;

                return GenericVariables.EnumerableEquals(gt.GenericVariables);
            }
            else if (other is GenericSelfType gst && gst.GenericSelf != null)
                return Equals(gst.GenericSelf);

            return false;
        }

        public override string ToString()
            => DataType.ToString() + "<" + string.Join(", ", GenericVariables.Select(x => x.Name)) + ">";
    }

    // represents a generic function group
    class GenericGroup : DataType
    {
        public readonly string Name;
        public readonly List<GenericType> GenericFunctions;

        public GenericGroup(string name, GenericType type1, GenericType type2)
        {
            Name = name;
            GenericFunctions = new List<GenericType> { type1, type2 };
        }

        private GenericGroup(string name, List<GenericType> genericFunctions)
        {
            Name = name;
            GenericFunctions = genericFunctions;
        }

        public override TypeClassifier Classify() => TypeClassifier.GENERIC_GROUP;

        public override DataType ConstCopy() => new GenericGroup(Name, GenericFunctions) { Constant = true };

        public bool AddGeneric(GenericType gt)
        {
            if (gt.DataType is FunctionType ft)
            {
                foreach (var function in GenericFunctions.Select(x => (FunctionType)x.DataType))
                {
                    if (!FunctionGroup.CanDistinguish(ft, function))
                        return false;
                }

                GenericFunctions.Add(gt);
                return true;
            }
            else
                return false;
        }

        public bool GetFunction(ArgumentList args, out FunctionType result)
        {
            foreach (var generic in GenericFunctions)
            {
                if (generic.Infer(args, out List<DataType> inferredTypes))
                {
                    generic.CreateGeneric(inferredTypes, out DataType dt);

                    result = (FunctionType)dt;
                    return true;
                }
            }

            result = null;
            return false;
        }

        protected override bool _coerce(DataType other)
        {
            if (other is GenericType gt)
                return gt.DataType is FunctionType && GenericFunctions.Any(x => x.Equals(other));
            else if (other is GenericGroup)
                return _equals(other);
            else
                return false;
        }

        protected override bool _equals(DataType other)
        {
            if (other is GenericGroup gg)
                return GenericFunctions.EnumerableEquals(gg.GenericFunctions);
            else
                return false;
        }

        public override string ToString() => $"GenericGroup[{RemovePrefix(Name)}]";
    }

    // used in the generic interface registry to determine whether or not a construct
    // is a match for the given type
    struct GenericBindDiscriminator
    {
        public readonly List<GenericVariable> GenericVariables;
        public readonly GenericType GenericBindType;

        public GenericBindDiscriminator(List<GenericVariable> genVars, GenericType gbt)
        {
            GenericVariables = genVars;
            GenericBindType = gbt;
        }

        public bool MatchType(DataType dt, out List<DataType> inferredTypes)
        {
            if (new[] { TypeClassifier.INCOMPLETE, TypeClassifier.GENERIC_GROUP, TypeClassifier.GENERIC,
                TypeClassifier.FUNCTION_GROUP, TypeClassifier.PACKAGE }.Contains(dt.Classify()))
            {
                inferredTypes = null;
                return false;
            }

            if (GenericBindType.InferFromType(dt, out inferredTypes))
            {
                // even tho we don't need the generic type, we still want to check the generate ;)
                GenericBindType.CreateGeneric(inferredTypes, out DataType _);

                return true;
            }
            else
            {
                inferredTypes = null;
                return false;
            }
        }
    }

    // represents an interface used in generic binding
    struct GenericBinding
    {
        public readonly GenericType Body;

        public List<InterfaceType> StandardImplements;
        public List<GenericType> GenericImplements;

        public GenericBinding(GenericType body)
        {
            Body = body;
            StandardImplements = new List<InterfaceType>();
            GenericImplements = new List<GenericType>();
        }
    }
}
