using System.Collections.Generic;

using Whirlwind.Parser;

namespace Whirlwind.Types
{
    // function returns data type of evaluated body and tests it
    delegate IDataType TemplateEvaluator(Dictionary<string, TemplateAlias> aliases, ASTNode body);

    // represents the various aliases of the templates (ie. the T in template<T>)
    class TemplateAlias : IDataType
    {
        public readonly IDataType ReplacementType;

        public TemplateAlias(IDataType replacementType)
        {
            ReplacementType = replacementType;
        }

        public bool Coerce(IDataType other) => false;

        public string Classify() => "TEMPLATE_ALIAS";
    }

    // represents the full template object (entire template method, module, ect.
    class TemplateType : IDataType
    {
        private readonly Dictionary<string, List<IDataType>> _templates;
        private readonly IDataType _templateType;
        private readonly TemplateEvaluator _evaluator;
        private readonly ASTNode _body;

        public TemplateType(Dictionary<string, List<IDataType>> templates, IDataType templateType, ASTNode body, TemplateEvaluator evaluator)
        {
            _templates = templates;
            _templateType = templateType;
            _evaluator = evaluator;
            _body = body;
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

                templateType = _evaluator(aliases, _body);
                return true;
            }
            

            templateType = null;
            return false;
        }

        public bool Coerce(IDataType other) => false;

        public string Classify() => "TEMPLATE";
    }
}
