using System.Collections.Generic;
using System.Linq;

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

        public bool Coerce(IDataType other) => false;
        public TypeClassifier Classify() => TypeClassifier.TEMPLATE_PLACEHOLDER;

        public bool Equals(IDataType other) => false;
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
    delegate IDataType TemplateEvaluator(Dictionary<string, IDataType> aliases);

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

    // represents the full template object (entire template method, obj, ect.)
    class TemplateType : IDataType
    {
        private readonly List<TemplateVariable> _templates;
        private readonly IDataType _dataType;

        private readonly TemplateEvaluator _evaluator;

        private readonly List<Dictionary<string, IDataType>> _generates;
        private readonly List<List<IDataType>> _variants;

        public TemplateType(List<TemplateVariable> templates, IDataType type, TemplateEvaluator evaluator)
        {
            _templates = templates;
            _dataType = type;

            _evaluator = evaluator;

            _generates = new List<Dictionary<string, IDataType>>();
            _variants = new List<List<IDataType>>();
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
                        if (!e1.Current.Restrictors.Contains(e2.Current) && e1.Current.Restrictors.Count > 0 /* check for empty restrictors */)
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

                templateType = _evaluator(aliases);
                return true;
            }
            

            templateType = null;
            return false;
        }

        public bool Infer(List<IDataType> parameters, out List<IDataType> inferredTypes)
        {
            // add template inference

            var filledTypes = new Dictionary<string, IDataType>();

            if (_dataType.Classify() == TypeClassifier.FUNCTION)
            {
                var fnType = (FunctionType)_dataType;

                
            }

            inferredTypes = new List<IDataType>();
            return false;
        }

        private List<string> _getAliasesCompleted(IDataType dt)
        {
            var aliasesCompleted = new List<string>();

            switch (dt.Classify())
            {
                case TypeClassifier.TEMPLATE_PLACEHOLDER:
                    aliasesCompleted.Add(((TemplatePlaceholder)dt).Name);
                    break;
                case TypeClassifier.TUPLE:
                    aliasesCompleted.AddRange(
                        ((TupleType)dt).Types.Select(x => _getAliasesCompleted(x)).SelectMany(x => x).Distinct()
                        );
                    break;
                case TypeClassifier.ARRAY:
                case TypeClassifier.LIST:
                    aliasesCompleted.AddRange(_getAliasesCompleted(((IIterable)dt).GetIterator()));
                    break;
                case TypeClassifier.DICT:
                    {
                        DictType dictType = (DictType)dt;

                        aliasesCompleted.AddRange(_getAliasesCompleted(dictType.KeyType));
                        aliasesCompleted.AddRange(_getAliasesCompleted(dictType.ValueType));
                    }
                    break;
                case TypeClassifier.FUNCTION:
                    {
                        FunctionType ft = (FunctionType)dt;

                        aliasesCompleted.AddRange(_getAliasesCompleted(ft.ReturnType));

                        aliasesCompleted.AddRange(ft.Parameters.Select(x => _getAliasesCompleted(x.DataType))
                            .SelectMany(x => x).Distinct().ToList());
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

        public bool Coerce(IDataType other) => false;

        public TypeClassifier Classify() => TypeClassifier.TEMPLATE;

        public bool Equals(IDataType other) => false;
    }
}
