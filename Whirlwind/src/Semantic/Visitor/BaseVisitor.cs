using Whirlwind.Parser;
using Whirlwind.Types;

using static Whirlwind.Semantic.Checker.Checker;

using System;
using System.Linq;
using System.Collections.Generic;

namespace Whirlwind.Semantic.Visitor
{
    partial class Visitor
    {
        private void _visitBase(ASTNode node)
        {
            if (node.Content[0].Name == "TOKEN")
            {
                SimpleType.SimpleClassifier dt = SimpleType.SimpleClassifier.INTEGER;

                bool unsigned = false;
                switch (((TokenNode)node.Content[0]).Tok.Type)
                {
                    case "INTEGER_LITERAL":
                        dt = SimpleType.SimpleClassifier.INTEGER;
                        unsigned = true;
                        break;
                    case "FLOAT_LITERAL":
                        dt = SimpleType.SimpleClassifier.FLOAT;
                        unsigned = true;
                        break;
                    case "BOOL_LITERAL":
                        dt = SimpleType.SimpleClassifier.BOOL;
                        break;
                    case "STRING_LITERAL":
                        dt = SimpleType.SimpleClassifier.STRING;
                        break;
                    case "CHAR_LITERAL":
                        dt = SimpleType.SimpleClassifier.CHAR;
                        break;
                    case "HEX_LITERAL":
                    case "BINARY_LITERAL":
                        _visitByteLiteral(((TokenNode)node.Content[0]).Tok);
                        return;
                    case "IDENTIFIER":
                        if (_table.Lookup(((TokenNode)node.Content[0]).Tok.Value, out Symbol sym))
                        {
                            if (sym.Modifiers.Contains(Modifier.CONSTEXPR))
                                _nodes.Add(new ConstexprNode(sym.Name, sym.DataType, sym.Value));
                            else
                                _nodes.Add(new IdentifierNode(sym.Name, sym.DataType));
                            return;
                        }
                        {
                            throw new SemanticException($"Undefined Symbol: `{((TokenNode)node.Content[0]).Tok.Value}`", node.Position);
                        }
                    case "THIS":
                        if (_table.Lookup("$THIS", out Symbol instance))
                        {
                            _nodes.Add(new IdentifierNode("$THIS", instance.DataType));
                            return;
                        }
                        {
                            throw new SemanticException("Use of `this` outside of object", node.Content[0].Position);
                        }
                    case "VAL":
                        {
                            if (_isVoid(_thenExprType))
                                throw new SemanticException("No valid chained value is accessible", node.Content[0].Position);

                            _nodes.Add(new ValueNode("Val", _thenExprType));
                            return;
                        }
                    case "NULL":
                        _nodes.Add(new ValueNode("Null", new VoidType()));
                        return;
                }

                _nodes.Add(new ValueNode("Literal", new SimpleType(dt, unsigned), ((TokenNode)node.Content[0]).Tok.Value));
            }
            else
            {
                switch (node.Content[0].Name)
                {
                    case "array":
                        var arr = _visitSet((ASTNode)node.Content[0]);
                        _nodes.Add(new ExprNode("Array", new ArrayType(arr.Item1, arr.Item2)));
                        if (arr.Item2 > 0)
                            PushForward(arr.Item2);
                        break;
                    case "list":
                        var list = _visitSet((ASTNode)node.Content[0]);
                        _nodes.Add(new ExprNode("List", new ListType(list.Item1)));
                        if (list.Item2 > 0)
                            PushForward(list.Item2);
                        break;
                    case "dict":
                        var dict = _visitDict((ASTNode)node.Content[0]);
                        _nodes.Add(new ExprNode("Dictionary", new DictType(dict.Item1, dict.Item2)));
                        // will default to array if value is too small, so check not needed
                        PushForward(dict.Item3);
                        break;
                    case "closure":
                        _visitClosure((ASTNode)node.Content[0]);
                        break;
                    case "sub_expr":
                        // base -> sub_expr -> ( expr ) 
                        // select expr
                        _visitExpr(((ASTNode)((ASTNode)node.Content[0]).Content[1]));
                        break;
                    case "type_cast":
                        _visitTypeCast((ASTNode)node.Content[0]);
                        break;
                    case "tuple":
                        _visitTuple((ASTNode)node.Content[0]);
                        break;
                    case "partial_func":
                        _visitPartialFunc((ASTNode)node.Content[0]);
                        break;
                }
            }
        }

        private Tuple<DataType, int> _visitSet(ASTNode node)
        {
            DataType elementType = new VoidType();
            int size = 0;
            foreach (var element in node.Content)
            {
                if (element.Name == "expr")
                {
                    _visitExpr((ASTNode)element);
                    _coerceSet(ref elementType, element.Position);
                    size++;
                }
            }
            return new Tuple<DataType, int>(elementType, size);
        }

        private Tuple<DataType, DataType, int> _visitDict(ASTNode node)
        {
            DataType keyType = new VoidType(), valueType = new VoidType();
            bool isKey = true;
            int size = 0;

            foreach (var element in node.Content)
            {
                if (element.Name == "expr")
                {
                    _visitExpr((ASTNode)element);
                    if (isKey)
                    {
                        _coerceSet(ref keyType, element.Position);

                        if (!Hashable(keyType))
                        {
                            throw new SemanticException("Unable to create map with unhashable type", element.Position);
                        }               
                        size++;
                    }
                    else
                    {
                        _coerceSet(ref valueType, element.Position);
                        // map pairs hold the key type
                        _nodes.Add(new ExprNode("KVPair", keyType));
                        // add 2 expr nodes to map pair
                        PushForward(2);
                    }
                    isKey = !isKey;
                }
            }

            return new Tuple<DataType, DataType, int>(keyType, valueType, size);
        }

        private void _coerceSet(ref DataType baseType, TextPosition pos)
        {
            DataType newType = _nodes.Last().Type;

            if (_isVoid(baseType))
            {
                baseType = newType;
                return;
            }
                
            if (!baseType.Coerce(newType))
            {
                if (newType.Coerce(baseType))
                    baseType = newType;
                else
                {
                    InterfaceType i1 = baseType.GetInterface(), i2 = newType.GetInterface();

                    if (i1.Implements.Count == 0 || i2.Implements.Count == 0)
                        throw new SemanticException("All values in a collection must be the same type", pos);

                    var matches = i1.Implements.Where(x => i2.Implements.Any(y => y.Equals(x)));

                    if (matches.Count() > 0)
                        baseType = matches.First();
                    else
                        throw new SemanticException("All values in a collection must be the same type", pos);
                }
                    
            }
        }

        private void _visitByteLiteral(Token token)
        {
            if (token.Type == "HEX_LITERAL")
            {
                if (token.Value.Length < 5 /* 5 to account for prefix */)
                {
                    _nodes.Add(new ValueNode("Literal", new SimpleType(SimpleType.SimpleClassifier.BYTE, true), token.Value));
                }
                else
                {
                    token.Value = token.Value.Substring(2);
                    string value = token.Value.Length % 2 == 0 ? token.Value : "0" + token.Value;
                    var pairs = Enumerable.Range(0, value.Length)
                        .Where(x => x % 2 == 0)
                        .Select(x => "0x" + value[x] + value[x + 1])
                        .Select(x => new ValueNode("Literal",
                            new SimpleType(SimpleType.SimpleClassifier.BYTE, true),
                            x))
                        .ToArray();
                    _nodes.Add(new ExprNode("Array",
                        new ArrayType(new SimpleType(SimpleType.SimpleClassifier.BYTE, true), pairs.Length)
                        ));
                    foreach (ValueNode node in pairs)
                    {
                        _nodes.Add(node);
                        MergeBack();
                    }
                }
            }
            else
            {
                if (token.Value.Length < 11 /* 11 to account for prefix */)
                {
                    _nodes.Add(new ValueNode("Literal", new SimpleType(SimpleType.SimpleClassifier.BYTE, true), token.Value));
                }
                else
                {
                    token.Value = token.Value.Substring(2);
                    string value = token.Value.Length % 8 == 0 ? token.Value :
                        string.Join("", Enumerable.Repeat("0", 8 - token.Value.Length % 8)) + token.Value;
                    var pairs = Enumerable.Range(0, value.Length)
                        .Where(x => x % 8 == 0)
                        .Select(x => "0b" + value.Substring(x, 8))
                        .Select(x => new ValueNode("Literal",
                            new SimpleType(SimpleType.SimpleClassifier.BYTE, true),
                            x))
                        .ToArray();
                    _nodes.Add(new ExprNode("Array",
                        new ArrayType(new SimpleType(SimpleType.SimpleClassifier.BYTE, true), pairs.Length)
                        ));
                    foreach (ValueNode node in pairs)
                    {
                        _nodes.Add(node);
                        MergeBack();
                    }
                }
            }
        }

        private void _visitClosure(ASTNode node)
        {
            var args = new List<Parameter>();
            DataType rtType = new VoidType();
            bool async = false;

            foreach (var item in node.Content)
            {
                switch (item.Name)
                {
                    case "args_decl_list":
                        args = _generateArgsDecl((ASTNode)item);
                        break;
                    case "closure_body":
                        _nodes.Add(new BlockNode("ClosureBody"));

                        rtType = _visitFuncBody((ASTNode)item, args);
                        break;
                }
            }

            var fType = new FunctionType(args, rtType, async); 

            _nodes.Add(new ExprNode("Closure", fType));
            PushForward();
        }

        private void _visitTypeCast(ASTNode node)
        {
            DataType dt = new VoidType();

            foreach (var item in node.Content)
            {
                if (item.Name == "types")
                    dt = _generateType((ASTNode)item);
                else if (item.Name == "expr")
                    _visitExpr((ASTNode)item);
            }

            if (!TypeCast(_nodes.Last().Type, dt))
                throw new SemanticException("Invalid type cast", node.Position);

            _nodes.Add(new ExprNode("TypeCast", dt));
            PushForward();
        }

        private void _visitTuple(ASTNode node)
        {
            int count = 0;
            var types = new List<DataType>();

            foreach (var subNode in node.Content)
            {
                if (subNode.Name == "expr")
                {
                    _visitExpr((ASTNode)subNode);
                    types.Add(_nodes.Last().Type);
                    count++;
                }
            }

            _nodes.Add(new ExprNode("Tuple", new TupleType(types)));
            PushForward(count);
        }

        private void _visitPartialFunc(ASTNode node)
        {
            _visitExpr((ASTNode)node.Content[1]);

            if (_nodes.Last().Type.Classify() != TypeClassifier.FUNCTION)
                throw new SemanticException("Unable to create partial function from non-function", node.Content[1].Position);

            FunctionType fnType = (FunctionType)_nodes.Last().Type;

            List<int> removedArgs = new List<int>();
            foreach (var item in node.Content.Where(x => x.Name != "TOKEN").Select((x, i) => new { Value = x, Index = i - 1 }))
            {
                if (((ASTNode)item.Value).Content[0].Name == "TOKEN")
                    continue;

                ASTNode expr = (ASTNode)((ASTNode)item.Value).Content[0];

                if (item.Value.Name == "partial_arg" && expr.Name == "expr")
                {
                    int ndx = fnType.Parameters.Count <= item.Index && fnType.Parameters.Last().Indefinite ? fnType.Parameters.Count - 1 : item.Index;

                    if (ndx >= fnType.Parameters.Count)
                        throw new SemanticException("Unable to fill in non-existent argument", item.Value.Position);

                    _visitExpr(expr);

                    if (!fnType.Parameters[ndx].DataType.Coerce(_nodes.Last().Type))
                        throw new SemanticException("Invalid value for the given argument", item.Value.Position);

                    // create the partial arguments
                    _nodes.Add(new ExprNode($"PartialArg{ndx}", _nodes.Last().Type));
                    PushForward();

                    removedArgs.Add(ndx);
                }
            }

            var newParameters = new List<Parameter>(fnType.Parameters);

            foreach (var item in removedArgs.Select((x, i) => new { Value = x, Index = i }))
            {
                newParameters.RemoveAt(item.Value - item.Index);
            }            

            _nodes.Add(new ExprNode("PartialFunction", new FunctionType(newParameters, fnType.ReturnType, fnType.Async)));

            // push forward root and args
            PushForward(removedArgs.Count + 1);
        }
    }
}
