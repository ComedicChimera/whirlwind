using System.Collections.Generic;
using System.Linq;
using System.Reflection;

using Whirlwind.Semantic;

namespace Whirlwind.Types
{
    // represents the template placeholder in the template signature
    class TemplatePlaceholder : IDataType
    {
        public readonly string Name;
        
        public TemplatePlaceholder(string name)
        {
            Name = name;
        }

        // the actual type of placeholder is irrelevant
        public bool Coerce(IDataType other) => true;
        public TypeClassifier Classify() => TypeClassifier.TEMPLATE_PLACEHOLDER;

        public bool Equals(IDataType other)
        {
            if (other.Classify() == TypeClassifier.TEMPLATE_PLACEHOLDER)
                return Name == ((TemplatePlaceholder)other).Name;

            return false;
        }
    }

    // represents the various aliases of the templates (ie. the T in template<T>)
    class TemplateAlias : IDataType
    {
        public readonly IDataType ReplacementType;

        public TemplateAlias(IDataType replacementType)
        {
            ReplacementType = replacementType;
        }

        public bool Coerce(IDataType other) => false;
        public TypeClassifier Classify() => TypeClassifier.TEMPLATE_ALIAS;

        public bool Equals(IDataType other) => false;
    }

    // function used to evaluate template body
    delegate TemplateGenerate TemplateEvaluator(Dictionary<string, IDataType> aliases, TemplateType parent);

    // a struct containing the template name and its restrictors
    struct TemplateVariable
    {
        public readonly string Name;
        public readonly List<IDataType> Restrictors;

        public TemplateVariable(string name, List<IDataType> restrictors)
        {
            Name = name;
            Restrictors = restrictors;
        }
    }

    // represents a single template generate instance
    struct TemplateGenerate
    {
        public IDataType DataType;
        public BlockNode Block;

        public TemplateGenerate(IDataType dt, BlockNode block)
        {
            DataType = dt;
            Block = block;
        }
    }

    // represents the full template object (entire template method, obj, ect.)
    class TemplateType : IDataType
    {
        private readonly List<TemplateVariable> _templates;
        private readonly TemplateEvaluator _evaluator;
        private readonly List<List<IDataType>> _variants;

        public IDataType DataType { get; private set; }
        public List<TemplateGenerate> Generates { get; private set; }

        public TemplateType(List<TemplateVariable> templates, IDataType type, TemplateEvaluator evaluator)
        {
            _templates = templates;
            DataType = type;

            _evaluator = evaluator;

            _variants = new List<List<IDataType>>();

            Generates = new List<TemplateGenerate>();
        }

        public bool CreateTemplate(List<IDataType> dataTypes, out IDataType templateType)
        {
            if (dataTypes.Count == _templates.Count)
            {
                var aliases = new Dictionary<string, IDataType>();

                using (var e1 = _templates.GetEnumerator())
                using (var e2 = dataTypes.GetEnumerator())
                {
                    while (e1.MoveNext() && e2.MoveNext())
                    {
                        if (e1.Current.Restrictors.Count > 0 /* check for empty restrictors */ 
                            && !e1.Current.Restrictors.Any(y => y.Coerce(e2.Current)))
                        {
                            templateType = null;
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
                templateType = generate.DataType;
                return true;
            }
            

            templateType = null;
            return false;
        }

        public bool Infer(List<IDataType> parameters, out List<IDataType> inferredTypes)
        {
            switch (DataType.Classify())
            {
                case TypeClassifier.FUNCTION:
                    return _inferFromFunction((FunctionType)DataType, parameters, out inferredTypes);
                case TypeClassifier.OBJECT:
                    if (((ObjectType)DataType).GetConstructor(parameters, out FunctionType constructor))
                        return _inferFromFunction(constructor, parameters, out inferredTypes);
                    break;
            }

            inferredTypes = new List<IDataType>();
            return false;
        }

        private bool _inferFromFunction(FunctionType fnType, List<IDataType> parameters, out List<IDataType> inferredTypes)
        {
            var completedAliases = new Dictionary<string, IDataType>();

            using (var e1 = fnType.Parameters.GetEnumerator())
            using (var e2 = parameters.GetEnumerator())
            {
                while (e1.MoveNext() && e2.MoveNext())
                {
                    var aliases = _getCompletedAliases(e1.Current.DataType);

                    foreach (var alias in aliases)
                    {
                        if (completedAliases.ContainsKey(alias))
                        {
                            if (!completedAliases[alias].Coerce(e2.Current))
                            {
                                if (e2.Current.Coerce(completedAliases[alias]))
                                    completedAliases[alias] = e2.Current;
                                else
                                {
                                    inferredTypes = new List<IDataType>();
                                    return false;
                                }

                            }
                        }
                        else
                            completedAliases[alias] = e2.Current;
                    }
                }
            }

            if (completedAliases.Count == _templates.Count)
            {
                _templates.Where(y => y.Name == "T").First().Restrictors.Any(y => y.Coerce(completedAliases["T"]));

                if (completedAliases.All(x => {
                    var res = _templates.Where(y => y.Name == x.Key).First().Restrictors;
                    return res.Count == 0 || res.Any(y => y.Coerce(x.Value));
                    }))
                {
                    inferredTypes = _templates.Select(x => completedAliases[x.Name]).ToList();
                    return true;
                }
            }

            inferredTypes = new List<IDataType>();
            return false;
        }

        private List<string> _getCompletedAliases(IDataType dt)
        {
            var aliasesCompleted = new List<string>();

            switch (dt.Classify())
            {
                case TypeClassifier.TEMPLATE_PLACEHOLDER:
                    aliasesCompleted.Add(((TemplatePlaceholder)dt).Name);
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
                    aliasesCompleted.AddRange(_getCompletedAliases(((PointerType)dt).Type));
                    break;
                case TypeClassifier.REFERENCE:
                    aliasesCompleted.AddRange(_getCompletedAliases(((ReferenceType)dt).DataType));
                    break;
                case TypeClassifier.OBJECT:
                    {
                        var members = (Dictionary<string, Symbol>)typeof(ObjectType).GetField("_members").GetValue(dt);

                        aliasesCompleted.AddRange(members.SelectMany(x => _getCompletedAliases(x.Value.DataType)));
                    }
                    break;
                case TypeClassifier.STRUCT:
                    aliasesCompleted.AddRange(((StructType)dt).Members.SelectMany(x => _getCompletedAliases(x.Value)));
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

        public bool AddVariant(List<IDataType> dataTypes)
        {
            if (dataTypes.Count != _templates.Count)
                return false;

            if (_variants.Contains(dataTypes))
                return false;

            using (var e1 = _templates.Select(x => x.Restrictors).GetEnumerator())
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

        public bool Coerce(IDataType other) => Equals(other);

        public TypeClassifier Classify() => TypeClassifier.TEMPLATE;

        public bool Equals(IDataType other)
        {
            if (other.Classify() == TypeClassifier.TEMPLATE)
            {
                var tt = (TemplateType)other;

                if (!DataType.Equals(tt.DataType))
                    return false;

                if (_templates.Count == tt._templates.Count)
                {
                    using (var e1 = _templates.GetEnumerator())
                    using (var e2 = tt._templates.GetEnumerator())
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
    }
}
