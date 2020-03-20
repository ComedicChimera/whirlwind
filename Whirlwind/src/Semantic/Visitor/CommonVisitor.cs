using System.Collections.Generic;
using System.Linq;

using Whirlwind.Syntax;
using Whirlwind.Types;

using static Whirlwind.Semantic.Checker.Checker;

namespace Whirlwind.Semantic.Visitor
{
    partial class Visitor
    {
        public void _visitIterator(ASTNode node, bool constant)
        {
            // knows last node is expr
            _visitExpr((ASTNode)node.Content.Last());

            var iterable = _nodes.Last().Type;
            DataType iteratorType;

            if (_getImpl("iterable", out DataType iterableDt))
            {
                if (iterableDt is InterfaceType iterableInterf)
                {
                    if (Iterable(iterable, iterableInterf))
                    {
                        iteratorType = _getIterableElementType(iterable);

                        if (iteratorType is NoneType)
                            throw new SemanticException("Unusable iterator type; check validity of iterable implementation", node.Position);
                    }
                    else
                        throw new SemanticException("Unable to create iterator over type of " + iterable.ToString(), node.Position);
                }
                else
                    throw new SemanticException("Unusable implementation for type `iterable`", node.Position);
            }
            else
                throw new SemanticException("Missing implementation for type `iterable`", node.Position);
            

            string[] identifiers = node.Content
                .Where(x => x.Name == "iter_var")
                .Select(x => ((ASTNode)x).Content[0])
                .Select(x => ((TokenNode)x).Tok.Value)
                .ToArray();

            var iteratorTypes = iteratorType.Classify() == TypeClassifier.TUPLE ? ((TupleType)iteratorType).Types : 
                new List<DataType>() { iteratorType };

            if (constant)
                iteratorTypes = iteratorTypes.Select(x => x.ConstCopy()).ToList();

            if (identifiers.Length != iteratorTypes.Count)
                throw new SemanticException("Base iterator and its aliases don't match", node.Position);

            _nodes.Add(new ExprNode("Iterator", new NoneType()));
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
            if (iterable is IIterableType)
                return (iterable as IIterableType).GetIterator();
            // should never fail - not a true overload so check not required
            else if (iterable.GetInterface().GetFunction("iter", out Symbol method))
            {
                DataType getMethodDataType(DataType dt)
                {
                    if (dt is FunctionGroup fg && fg.GetFunction(new ArgumentList(), out FunctionType ft))
                        return ft;
                    else if (!(dt is FunctionType))
                        return new NoneType();

                    return dt;
                }

                DataType mdt = getMethodDataType(method.DataType);

                if (!((FunctionType)mdt).ReturnType.GetInterface().GetFunction("next", out method))
                    return new NoneType();

                mdt = getMethodDataType(method.DataType);

                // all iterable next methods return a specific element type (T, bool)
                var elementType = ((FunctionType)mdt).ReturnType;

                return ((TupleType)elementType).Types[0];
            }

            return new NoneType();
        }

        private DataType _generateGeneric(GenericType baseType, ASTNode genericSpecifier)
        {
            // generic_spec -> type_list
            var typeList = _generateTypeList((ASTNode)genericSpecifier.Content[1]);
            
            if (baseType.CreateGeneric(typeList, out DataType dt))
                return dt;
            else
                throw new SemanticException("Invalid type specifier for " + baseType.ToString(), genericSpecifier.Position);

        }

        private bool _getImpl(string implName, out DataType implDt)
        {
            if (_impls.ContainsKey(implName))
            {
                implDt = _impls[implName];
                return true;
            }

            implDt = null;
            return false;
        }
    }
}
