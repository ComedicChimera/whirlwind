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

    // represents the full generic object (entire generic method, obj, ect.)
    class GenericType : DataType
    {
        private readonly List<GenericVariable> _generics;
        private readonly GenericEvaluator _evaluator;
        private readonly List<List<DataType>> _variants;

        public DataType DataType { get; private set; }
        public List<GenericGenerate> Generates { get; private set; }

        public GenericType(List<GenericVariable> generics, DataType type, GenericEvaluator evaluator)
        {
            _generics = generics;
            DataType = type;

            _evaluator = evaluator;

            _variants = new List<List<DataType>>();

            Generates = new List<GenericGenerate>();

            Constant = true;
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
                    if (!_checkCompletedAliases(e1.Current.DataType, e2.Current, completedAliases))
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
                    if (!_checkCompletedAliases(matches.First().DataType, nArg.Value, completedAliases))
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

        private bool _checkCompletedAliases(DataType paramType, DataType argType, Dictionary<string, DataType> completedAliases)
        {
            var aliases = _getCompletedAliases(paramType);

            foreach (var alias in aliases)
            {
                if (completedAliases.ContainsKey(alias))
                {
                    if (!completedAliases[alias].Coerce(argType))
                    {
                        if (argType.Coerce(completedAliases[alias]))
                            completedAliases[alias] = argType;
                        else
                            return false;
                    }
                }
                else
                    completedAliases[alias] = argType;
            }

            return true;
        }

        private List<string> _getCompletedAliases(DataType dt)
        {
            var aliasesCompleted = new List<string>();

            switch (dt.Classify())
            {
                case TypeClassifier.GENERIC_PLACEHOLDER:
                    aliasesCompleted.Add(((GenericPlaceholder)dt).Name);
                    break;
                case TypeClassifier.TUPLE:
                    aliasesCompleted.AddRange(
                        ((TupleType)dt).Types.SelectMany(x => _getCompletedAliases(x)).Distinct()
                        );
                    break;
                case TypeClassifier.ARRAY:
                case TypeClassifier.LIST:
                    aliasesCompleted.AddRange(_getCompletedAliases(((IIterable)dt).GetIterator()));
                    break;
                case TypeClassifier.DICT:
                    {
                        DictType dictType = (DictType)dt;

                        aliasesCompleted.AddRange(_getCompletedAliases(dictType.KeyType));
                        aliasesCompleted.AddRange(_getCompletedAliases(dictType.ValueType));
                    }
                    break;
                case TypeClassifier.FUNCTION:
                    {
                        FunctionType ft = (FunctionType)dt;

                        aliasesCompleted.AddRange(_getCompletedAliases(ft.ReturnType));

                        aliasesCompleted.AddRange(ft.Parameters.SelectMany(x => _getCompletedAliases(x.DataType)).Distinct().ToList());
                    }
                    break;
                case TypeClassifier.POINTER:
                    aliasesCompleted.AddRange(_getCompletedAliases(((PointerType)dt).DataType));
                    break;
                case TypeClassifier.REFERENCE:
                    aliasesCompleted.AddRange(_getCompletedAliases(((ReferenceType)dt).DataType));
                    break;
                case TypeClassifier.STRUCT:
                    aliasesCompleted.AddRange(((StructType)dt).Members.SelectMany(x => _getCompletedAliases(x.Value.DataType)));
                    break;
                case TypeClassifier.INTERFACE:
                    {
                        var methods = (Dictionary<Symbol, bool>)typeof(InterfaceType).GetField("_methods").GetValue(dt);

                        aliasesCompleted.AddRange(methods.SelectMany(x => _getCompletedAliases(x.Key.DataType)));
                    }
                    break;
            }

            return aliasesCompleted;
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
