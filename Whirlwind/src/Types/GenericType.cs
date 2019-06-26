using System.Collections.Generic;
using System.Linq;

using Whirlwind.Semantic;
using Whirlwind.Parser;

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
            => new GenericPlaceholder(Name); // implict const
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
        public DataType DataType;
        public BlockNode Block;

        public GenericGenerate(DataType dt, BlockNode block)
        {
            DataType = dt;
            Block = block;
        }
    }

    struct GenericInterface
    {
        public ASTNode Body;

        public List<InterfaceType> StandardImplements;
        public List<GenericType> GenericImplements;

        public GenericInterface(ASTNode body)
        {
            Body = body;
            StandardImplements = new List<InterfaceType>();
            GenericImplements = new List<GenericType>();
        }
    }

    // represents the full generic object (entire generic method, obj, ect.)
    class GenericType : DataType
    {
        private readonly List<GenericVariable> _generics;
        private readonly GenericEvaluator _evaluator;
        private readonly List<List<DataType>> _variants;

        public DataType DataType { get; private set; }
        public List<GenericGenerate> Generates { get; private set; }

        public GenericInterface GenericInterface;

        public GenericType(List<GenericVariable> generics, DataType type, GenericEvaluator evaluator)
        {
            _generics = generics;
            DataType = type;

            _evaluator = evaluator;

            _variants = new List<List<DataType>>();

            Generates = new List<GenericGenerate>();

            Constant = true;

            GenericInterface = new GenericInterface();
        }

        public bool CreateGeneric(List<DataType> dataTypes, out DataType genericType)
        {
            if (dataTypes.Count == _generics.Count)
            {
                var aliases = new Dictionary<string, DataType>();

                using (var e1 = _generics.GetEnumerator())
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
                        {
                            aliases.Add(e1.Current.Name, e2.Current);
                        }
                    }
                }

                var generate = _evaluator(aliases, this);

                Generates.Add(generate);
                genericType = generate.DataType;
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

            if (completedAliases.Count == _generics.Count)
            {
                _generics.Where(y => y.Name == "T").First().Restrictors.Any(y => y.Coerce(completedAliases["T"]));

                if (completedAliases.All(x => {
                    var res = _generics.Where(y => y.Name == x.Key).First().Restrictors;
                    return res.Count == 0 || res.Any(y => y.Coerce(x.Value));
                    }))
                {
                    inferredTypes = _generics.Select(x => completedAliases[x.Name]).ToList();
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
                        if (tb.Coerce(ca))
                        {
                            completedAliases[placeholderName] = tb;
                            return true;
                        }

                        return false;
                    }
                }
                else
                {
                    completedAliases[placeholderName] = tb;
                    return true;
                }
            }

            if (ta.Classify() == tb.Classify())
                return false;

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
                case TypeClassifier.REFERENCE:
                    return _getCompletedAliases(((ReferenceType)ta).DataType, ((ReferenceType)tb).DataType, completedAliases);
                case TypeClassifier.STRUCT:
                    {
                        StructType sta = (StructType)ta, stb = (StructType)tb;

                        return sta.Members.All(x => _getCompletedAliases(x.Value.DataType, stb.Members[x.Key].DataType, completedAliases));
                    }
                case TypeClassifier.INTERFACE:
                    {
                        var methods = (Dictionary<Symbol, bool>)typeof(InterfaceType).GetField("_methods").GetValue(ta);

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

        public bool AddVariant(List<DataType> dataTypes)
        {
            if (dataTypes.Count != _generics.Count)
                return false;

            if (_variants.Contains(dataTypes))
                return false;

            using (var e1 = _generics.Select(x => x.Restrictors).GetEnumerator())
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

        protected override bool _equals(DataType other)
        {
            if (other.Classify() == TypeClassifier.GENERIC)
            {
                var tt = (GenericType)other;

                if (!DataType.Equals(tt.DataType))
                    return false;

                if (_generics.Count == tt._generics.Count)
                {
                    using (var e1 = _generics.GetEnumerator())
                    using (var e2 = tt._generics.GetEnumerator())
                    {
                        while (e1.MoveNext() && e2.MoveNext())
                        {
                            if (e1.Current.Name != e2.Current.Name || 
                                e1.Current.Restrictors.Count != e2.Current.Restrictors.Count ||
                                !Enumerable.Range(0, e1.Current.Restrictors.Count)
                                .All(i => e1.Current.Restrictors[i].Equals(e2.Current.Restrictors[i]))
                            )
                            {
                                return false;
                            }
                        }
                    }
                }

                return true;
            }

            return false;
        }

        public override DataType ConstCopy()
        {
            var tt = new GenericType(_generics, DataType, _evaluator)
            {
                Generates = Generates
            }; // implicit const

            foreach (var variant in _variants)
                tt._variants.Add(variant);

            return tt;
        }

        public bool CompareGenerics(List<GenericVariable> other)
        {
            return _generics.Count == other.Count &&
                _generics.All(x => other.Where(y => y.Equals(x)).Count() > 0);
        }
    }
}
