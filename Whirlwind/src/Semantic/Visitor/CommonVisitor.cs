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
            DataType iteratorType;

            if (Iterable(iterable))
            {
                iteratorType = _getIterableElementType(iterable);
            }
            else
                throw new SemanticException("Unable to create iterator over non-iterable value", node.Position);

            string[] identifiers = node.Content
                .Where(x => x.Name == "iter_var")
                .Select(x => ((ASTNode)x).Content[0])
                .Select(x => ((TokenNode)x).Tok.Value)
                .ToArray();

            var iteratorTypes = iteratorType.Classify() == TypeClassifier.TUPLE ? ((TupleType)iteratorType).Types : 
                new List<DataType>() { iteratorType };

            // create constant copies of all the types
            iteratorTypes = iteratorTypes.Select(x => x.ConstCopy()).ToList();

            if (identifiers.Length != iteratorTypes.Count)
                throw new SemanticException("Base iterator and its aliases don't match", node.Position);

            _nodes.Add(new ExprNode("Iterator", new SimpleType()));
            // push forward base expression
            PushForward();

            for (int i = 0; i < identifiers.Length; i++)
            {
                if (identifiers[i] != "_")
                {
                    if (!_table.AddSymbol(new Symbol(identifiers[i], iteratorTypes[i])))
                        throw new SemanticException("Iterator cannot contain duplicate aliases",
                            node.Content.Where(x => x.Name == "iter_var").Select(x => ((ASTNode)x).Content[0].Position).ElementAt(i)
                        );
                }
                    

                _nodes.Add(new IdentifierNode(identifiers[i], iteratorTypes[i]));
            }

            MergeBack(identifiers.Length);
        }

        private DataType _getIterableElementType(DataType iterable)
        {
            if (iterable is IIterable)
                return (iterable as IIterable).GetIterator();
            // should never fail - not a true overload so check not required
            else if (iterable.GetInterface().GetFunction("__<-__", out Symbol method))
            {
                DataType mdt = method.DataType;

                if (mdt is FunctionGroup fg && fg.GetFunction(new ArgumentList(), out FunctionType ft))
                    mdt = ft;
                else
                    return new SimpleType();                   

                // all iterable __next__ methods return a specific element type (T, bool)
                var elementType = ((FunctionType)method.DataType).ReturnType;

                return ((TupleType)elementType).Types[0];
            }
            return new SimpleType();
        }

        private DataType _generateTemplate(TemplateType baseType, ASTNode templateSpecifier)
        {
            // template_spec -> type_list
            var typeList = _generateTypeList((ASTNode)templateSpecifier.Content[1]);
            
            if (baseType.CreateTemplate(typeList, out DataType dt))
                return dt;
            else
                throw new SemanticException("Invalid type specifier for the given template", templateSpecifier.Position);

        }
    }
}
