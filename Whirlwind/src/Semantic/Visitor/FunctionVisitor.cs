using Whirlwind.Types;
using Whirlwind.Parser;

using System;
using System.Collections.Generic;

namespace Whirlwind.Semantic.Visitor
{
    partial class Visitor
    {
        public List<Parameter> _generateArgsDecl(ASTNode node)
        {
            return new List<Parameter>();
        }

        private Tuple<List<IDataType>, bool> _visitFuncBody(ASTNode node)
        {
            return new Tuple<List<IDataType>, bool>(new List<IDataType>(), false);
        }

        private List<ParameterValue> _generateArgsList(ASTNode node)
        {
            return new List<ParameterValue>();
        }
    }
}
