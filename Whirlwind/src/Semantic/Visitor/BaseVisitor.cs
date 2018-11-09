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
                SimpleType.DataType dt = SimpleType.DataType.VOID;
                bool unsigned = false;
                switch (((TokenNode)node.Content[0]).Tok.Type)
                {
                    case "INTEGER_LITERAL":
                        dt = SimpleType.DataType.INTEGER;
                        unsigned = true;
                        break;
                    case "FLOAT_LITERAL":
                        dt = SimpleType.DataType.FLOAT;
                        unsigned = true;
                        break;
                    case "BOOL_LITERAL":
                        dt = SimpleType.DataType.BOOL;
                        break;
                    case "STRING_LITERAL":
                        dt = SimpleType.DataType.STRING;
                        break;
                    case "CHAR_LITERAL":
                        dt = SimpleType.DataType.CHAR;
                        break;
                    case "HEX_LITERAL":
                    case "BINARY_LITERAL":
                        _visitByteLiteral(((TokenNode)node.Content[0]).Tok);
                        return;
                    case "IDENTIFIER":
                        if (((TokenNode)node.Content[0]).Tok.Value == "_")
                            throw new SemanticException("Unable to reference the `_` variable in an expression context", node.Content[0].Position);
                        if (_table.Lookup(((TokenNode)node.Content[0]).Tok.Value, out Symbol sym))
                        {
                            if (sym.Modifiers.Contains(Modifier.CONSTEXPR))
                                _nodes.Add(new ConstexprNode(sym.Name, sym.DataType, sym.Value));
                            else
                                _nodes.Add(new IdentifierNode(sym.Name, sym.DataType, sym.Modifiers.Contains(Modifier.CONSTANT)));
                            return;
                        }
                        {
                            throw new SemanticException($"Undefined Identifier: `{((TokenNode)node.Content[0]).Tok.Value}`", node.Position);
                        }

                    case "THIS":
                        if (_table.Lookup("$THIS", out Symbol instance))
                        {
                            _nodes.Add(new IdentifierNode("$THIS", instance.DataType, true));
                            return;
                        }
                        {
                            throw new SemanticException("Use of `this` outside of property", node.Content[0].Position);
                        }
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
                    case "map":
                        var map = _visitMap((ASTNode)node.Content[0]);
                        _nodes.Add(new ExprNode("Map", new MapType(map.Item1, map.Item2)));
                        // will default to array if value is too small, so check not needed
                        PushForward(map.Item3);
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
                    case "sizeof":
                        var typeExpr = (ASTNode)((ASTNode)node.Content[0]).Content[2];
                        _nodes.Add(new ExprNode("SizeOf", new SimpleType(SimpleType.DataType.INTEGER, true)));

                        if (typeExpr.Name == "types")
                            _nodes.Add(new ValueNode("DataType", _generateType(typeExpr)));
                        else
                            _visitExpr(typeExpr);

                        MergeBack();
                        break;
                    case "tuple":
                        _visitTuple((ASTNode)node.Content[0]);
                        break;
                }
            }
        }

        private Tuple<IDataType, int> _visitSet(ASTNode node)
        {
            IDataType elementType = new SimpleType();
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
            return new Tuple<IDataType, int>(elementType, size);
        }

        private Tuple<IDataType, IDataType, int> _visitMap(ASTNode node)
        {
            IDataType keyType = new SimpleType(), valueType = new SimpleType();
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
                        _nodes.Add(new ExprNode("MapPair", keyType));
                        // add 2 expr nodes to map pair
                        PushForward(2);
                    }
                    isKey = !isKey;
                }
            }

            return new Tuple<IDataType, IDataType, int>(keyType, valueType, size);
        }

        private void _coerceSet(ref IDataType baseType, TextPosition pos)
        {
            IDataType newType = _nodes.Last().Type;
            if (!baseType.Coerce(newType))
            {
                if (newType.Coerce(baseType))
                    baseType = newType;
                else
                    throw new SemanticException("All values in a collection must be the same type", pos);
            }

            if (baseType.Classify() == TypeClassifier.SIMPLE && ((SimpleType)baseType).Type == SimpleType.DataType.VOID)
                baseType = newType;
        }

        private void _visitByteLiteral(Token token)
        {
            if (token.Type == "HEX_LITERAL")
            {
                if (token.Value.Length < 5 /* 5 to account for prefix */)
                {
                    _nodes.Add(new ValueNode("Literal", new SimpleType(SimpleType.DataType.BYTE, true), token.Value));
                }
                else
                {
                    token.Value = token.Value.Substring(2);
                    string value = token.Value.Length % 2 == 0 ? token.Value : "0" + token.Value;
                    var pairs = Enumerable.Range(0, value.Length)
                        .Where(x => x % 2 == 0)
                        .Select(x => "0x" + value[x] + value[x + 1])
                        .Select(x => new ValueNode("Literal",
                            new SimpleType(SimpleType.DataType.BYTE, true),
                            x))
                        .ToArray();
                    _nodes.Add(new ExprNode("Array",
                        new ArrayType(new SimpleType(SimpleType.DataType.BYTE, true), pairs.Length)
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
                    _nodes.Add(new ValueNode("Literal", new SimpleType(SimpleType.DataType.BYTE, true), token.Value));
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
                            new SimpleType(SimpleType.DataType.BYTE, true),
                            x))
                        .ToArray();
                    _nodes.Add(new ExprNode("Array",
                        new ArrayType(new SimpleType(SimpleType.DataType.BYTE, true), pairs.Length)
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
            IDataType rtType = new SimpleType();
            bool async = false;

            foreach (var item in node.Content)
            {
                switch (item.Name)
                {
                    case "TOKEN":
                        if (((TokenNode)item).Tok.Type == "ASYNC")
                            async = true;
                        break;
                    case "args_decl_list":
                        args = _generateArgsDecl((ASTNode)item);
                        break;
                    case "func_body":
                        _nodes.Add(new BlockNode("FunctionBody"));
                        rtType = _visitFuncBody((ASTNode)item);
                        break;
                }
            }

            var fType = new FunctionType(args, rtType, async); 

            _nodes.Add(new ExprNode("Closure", fType));
            PushForward(2);
        }

        private void _visitTypeCast(ASTNode node)
        {
            IDataType dt = new SimpleType();

            foreach (var item in node.Content)
            {
                if (item.Name == "types")
                    dt = _generateType((ASTNode)item);
                else if (item.Name == "expr")
                    _visitExpr((ASTNode)item);
            }

            if (!TypeCast(dt, _nodes.Last().Type))
                throw new SemanticException("Invalid type cast", node.Position);

            _nodes.Add(new ExprNode("TypeCast", dt));
            PushForward();
        }

        private void _visitTuple(ASTNode node)
        {
            int count = 0;
            var types = new List<IDataType>();

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
    }
}
