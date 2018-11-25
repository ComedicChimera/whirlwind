using Whirlwind.Parser;
using Whirlwind.Types;
using static Whirlwind.Semantic.Checker.Checker;

using System.Collections.Generic;
using System.Linq;

namespace Whirlwind.Semantic.Visitor
{
    partial class Visitor
    {
        public void _visitIterator(ASTNode node)
        {
            // knows last node is expr
            _visitExpr((ASTNode)node.Content.Last());

            var iterable = _nodes.Last().Type;
            IDataType iteratorType;

            if (Iterable(iterable))
            {
                iteratorType = _getIterableElementType(iterable);
            }
            else
                throw new SemanticException("Unable to create iterator over non-iterable value", node.Position);

            string[] identifiers = node.Content
                .Where(x => x.Name == "TOKEN")
                .Select(x => ((TokenNode)x).Tok)
                .Where(x => x.Type == "IDENTIFIER")
                .Select(x => x.Value)
                .ToArray();

            var iteratorTypes = iteratorType.Classify() == TypeClassifier.TUPLE ? ((TupleType)iteratorType).Types : new List<IDataType>() { iteratorType };

            if (identifiers.Length != iteratorTypes.Count)
                throw new SemanticException("Base iterator and its aliases don't match", node.Position);

            _nodes.Add(new ExprNode("Iterator", new SimpleType()));
            // push forward base expression
            PushForward();

            for (int i = 0; i < identifiers.Length; i++)
            {
                _table.AddSymbol(new Symbol(identifiers[i], iteratorTypes[i], new List<Modifier>() { Modifier.CONSTANT }));

                _nodes.Add(new IdentifierNode(identifiers[i], iteratorTypes[i], true));
            }

            MergeBack(identifiers.Length);
        }

        private IDataType _getIterableElementType(IDataType iterable)
        {
            if (iterable is IIterable)
                return (iterable as IIterable).GetIterator();
            // should never fail - not a true overload so check not required
            else if (((ObjectInstance)iterable).GetProperty("__next__", out Symbol method))
            {
                // all iterable __next__ methods return a specific element type (Element<T>)
                var elementType = ((FunctionType)method.DataType).ReturnType;

                return ((StructType)elementType).Members["val"];
            }
            return new SimpleType();
        }

        private IDataType _generateTemplate(TemplateType baseType, ASTNode templateSpecifier)
        {
            // template_spec -> type_list
            var typeList = _generateTypeList((ASTNode)templateSpecifier.Content[1]);
            
            if (baseType.CreateTemplate(typeList, out IDataType dt))
                return dt;
            else
                throw new SemanticException("Invalid type specifier for the given template", templateSpecifier.Position);

        }
    }
}
