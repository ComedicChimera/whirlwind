using System.Collections.Generic;

using Whirlwind.Parser;

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

    // represents the full template object (entire template method, obj, ect.)
    class TemplateType : IDataType
    {
        private readonly Dictionary<string, List<IDataType>> _templates;
        private readonly IDataType _dataType;
        private readonly Dictionary<List<IDataType>, ASTNode> _variants;
        private readonly List<IDataType> _generates;

        public TemplateType(Dictionary<string, List<IDataType>> templates, IDataType type)
        {
            _templates = templates;
            _variants = new Dictionary<List<IDataType>, ASTNode>();
            _dataType = type;
        }

        public bool CreateTemplate(List<IDataType> dataTypes, out IDataType templateType)
        {
            if (dataTypes.Count == _templates.Count)
            {
                var aliases = new Dictionary<string, TemplateAlias>();

                using (var e1 = _templates.GetEnumerator())
                using (var e2 = dataTypes.GetEnumerator())
                {
                    while (e1.MoveNext() && e2.MoveNext())
                    {
                        if (!e1.Current.Value.Contains(e2.Current) && e1.Current.Value.Count > 0 /* check for empty type specifiers */)
                        {
                            templateType = null;
                            return false;
                        }
                        else
                        {
                            aliases.Add(e1.Current.Key, new TemplateAlias(e2.Current));
                        }
                    }
                }

                templateType = _evaluate(aliases);
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

        public bool AddVariant(List<IDataType> dataTypes, ASTNode variant_body)
        {
            if (dataTypes.Count != _templates.Count)
                return false;

            if (_variants.ContainsKey(dataTypes))
                return false;

            using (var e1 = _templates.Values.GetEnumerator())
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

            _variants[dataTypes] = variant_body;
            return true;
        }

        private IDataType _evaluate(Dictionary<string, TemplateAlias> aliases)
        {
            // add template signature evaluation

            return new SimpleType();
        }

        public bool Coerce(IDataType other) => false;

        public TypeClassifier Classify() => TypeClassifier.TEMPLATE;

        public bool Equals(IDataType other) => false;
    }
}
