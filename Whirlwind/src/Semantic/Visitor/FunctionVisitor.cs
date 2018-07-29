using Whirlwind.Types;
using Whirlwind.Parser;

using System.Collections.Generic;

namespace Whirlwind.Semantic.Visitor
{
    partial class Visitor
    {
        public List<Parameter> _generateArgsDecl(ASTNode node)
        {
            return new List<Parameter>();
        }

        private List<IDataType> _visitFuncBody(ASTNode node)
        {
            return new List<IDataType>();
        }

        private List<ParameterValue> _generateArgsList(ASTNode node)
        {
            return new List<ParameterValue>();
        }
    }
}
