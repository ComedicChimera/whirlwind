using System;
using System.Linq;
using System.Collections.Generic;

using Whirlwind.Syntax;
using Whirlwind.Types;

using static Whirlwind.Semantic.Checker.Checker;

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
                        {
                            var tokVal = ((TokenNode)node.Content[0]).Tok.Value;
                            bool defLong = tokVal.Contains('l');

                            unsigned = tokVal.Contains('u');

                            if (ulong.TryParse(tokVal.TrimEnd('u', 'l'), out ulong result))
                            {
                                if (unsigned)
                                {
                                    if (!defLong && result <= UInt32.MaxValue)
                                        dt = SimpleType.SimpleClassifier.INTEGER;
                                    else
                                        dt = SimpleType.SimpleClassifier.LONG;
                                }
                                else
                                {
                                    if (result > Int64.MaxValue)
                                    {
                                        dt = SimpleType.SimpleClassifier.LONG;
                                        unsigned = true;
                                    }
                                    else if (defLong || result > UInt32.MaxValue)
                                        dt = SimpleType.SimpleClassifier.LONG;
                                    else if (result > Int32.MaxValue)
                                    {
                                        dt = SimpleType.SimpleClassifier.INTEGER;
                                        unsigned = true;
                                    }
                                    else
                                        dt = SimpleType.SimpleClassifier.INTEGER;
                                }
                            }
                            else
                                throw new SemanticException("Integer literal too long", node.Content[0].Position);
                        }
                        break;
                    case "FLOAT_LITERAL":
                        {
                            var tokVal = ((TokenNode)node.Content[0]).Tok.Value;

                            if (double.TryParse(tokVal.TrimEnd('d'), out double result))
                            {
                                if (tokVal.Contains('d') || result > float.MaxValue)
                                    dt = SimpleType.SimpleClassifier.DOUBLE;
                                else
                                    dt = SimpleType.SimpleClassifier.FLOAT;
                            }
                            else
                                throw new SemanticException("Integer literal too long", node.Content[0].Position);
                        }
                        break;
                    case "BOOL_LITERAL":
                        dt = SimpleType.SimpleClassifier.BOOL;
                        break;
                    case "STRING_LITERAL":
                        dt = SimpleType.SimpleClassifier.STRING;

                        if (!VerifyString(((TokenNode)node.Content[0]).Tok.Value))
                            throw new SemanticException("Malformed string literal", node.Content[0].Position);

                        unsigned = true;
                        break;
                    case "CHAR_LITERAL":
                        dt = SimpleType.SimpleClassifier.CHAR;
                        unsigned = true;

                        if (!VerifyChar(((TokenNode)node.Content[0]).Tok.Value))
                            throw new SemanticException("Malformed char literal", node.Content[0].Position);
                        break;
                    case "HEX_LITERAL":
                    case "BINARY_LITERAL":
                        _visitByteLiteral(((TokenNode)node.Content[0]).Tok);
                        return;
                    case "IDENTIFIER":
                        if (_table.Lookup(((TokenNode)node.Content[0]).Tok.Value, out Symbol sym))
                        {
                            var symDt = sym.DataType;

                            if (symDt is GenericSelfType gst)
                            {
                                if (gst.GenericSelf == null)
                                    throw new SemanticException("Unable to use incomplete type in expression", node.Content[0].Position);
                                else
                                    symDt = gst.GenericSelf;
                            }
                            else if (symDt is GenericSelfInstanceType gsit)
                            {
                                if (gsit.GenericSelf == null)
                                    throw new SemanticException("Unable to use incomplete type in expression", node.Content[0].Position);
                                else
                                    symDt = gsit.GenericSelf;
                            }
                            else if (symDt is SelfType st)
                            {
                                if (st.Initialized)
                                    symDt = st.DataType;
                                else
                                    throw new SemanticException("Unable to use incomplete type in expression", node.Content[0].Position);
                            }

                            if (sym.Modifiers.Contains(Modifier.CONSTEXPR))
                                _nodes.Add(new ConstexprNode(sym.Name, symDt, sym.Value));
                            else
                                _nodes.Add(new IdentifierNode(sym.Name, symDt));

                            return;
                        }
                        else if (_couldTypeClassContextExist)
                            throw new SemanticContextException();
                        // when the context is evaluated the failed look up is checked prior
                        // no need to check here
                        else if (_typeClassContext != null)
                        {
                            var name = ((TokenNode)node.Content[0]).Tok.Value;

                            var customMatches = _typeClassContext.Instances.Where(x => x is CustomNewType)
                                .Select(x => (CustomNewType)x)
                                .Where(c => c.Name == name);

                            if (customMatches.Count() == 0)
                                throw new SemanticException($"Undefined symbol: `{name}`", node.Position);

                            _nodes.Add(new IdentifierNode(_typeClassContext.Name, _typeClassContext));
                            _nodes.Add(new IdentifierNode(name, customMatches.First()));

                            _nodes.Add(new ExprNode("StaticGet", _nodes.Last().Type));
                            PushForward(2);

                            return;
                        }
                        else
                            throw new SemanticException($"Undefined symbol: `{((TokenNode)node.Content[0]).Tok.Value}`", node.Position);
                    case "THIS":
                        if (_table.Lookup("this", out Symbol instance))
                        {
                            _nodes.Add(new IdentifierNode("this", instance.DataType));
                            return;
                        }
                        {
                            throw new SemanticException("Unable to use `this` outside of type method or constructor", node.Content[0].Position);
                        }
                    case "VALUE":
                        {
                            if (_isVoidOrNull(_thenExprType))
                                throw new SemanticException("No valid chained value is accessible", node.Content[0].Position);

                            _nodes.Add(new ValueNode("Value", _thenExprType));
                            return;
                        }
                    case "NULL":
                        _nodes.Add(new ValueNode("Null", new NullType()));
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
                    case "comprehension":
                        _visitComprehension((ASTNode)node.Content[0]);
                        break;
                    case "lambda":
                        _visitLambda((ASTNode)node.Content[0]);
                        break;
                    case "sub_expr":
                        // base -> sub_expr -> ( expr ) 
                        // select expr
                        _visitExpr(((ASTNode)((ASTNode)node.Content[0]).Content[1]));
                        break;
                    case "tuple":
                        _visitTuple((ASTNode)node.Content[0]);
                        break;
                    case "partial_func":
                        _visitPartialFunc((ASTNode)node.Content[0]);
                        break;
                    case "super_call":
                        _visitSuperCall((ASTNode)node.Content[0]);
                        break;
                    case "sizeof_call":
                        {
                            var size = _generateType((ASTNode)node.Content[2]).SizeOf();

                            _nodes.Add(new ValueNode("Literal", new SimpleType(SimpleType.SimpleClassifier.INTEGER, true), size.ToString()));
                        }
                        break;
                }
            }
        }

        private Tuple<DataType, int> _visitSet(ASTNode node)
        {
            DataType elementType = new NoneType();
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
            DataType keyType = new NoneType(), valueType = new NoneType();
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

            if (_isVoidOrNull(baseType))
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
            int tokenCount = token.Value.Length - 2;
            SimpleType.SimpleClassifier sc;

            if (token.Type == "HEX_LITERAL")
            {
                if (tokenCount <= 2)
                    sc = SimpleType.SimpleClassifier.BYTE;
                else if (tokenCount <= 4)
                    sc = SimpleType.SimpleClassifier.SHORT;
                else if (tokenCount <= 8)
                    sc = SimpleType.SimpleClassifier.INTEGER;
                else
                    sc = SimpleType.SimpleClassifier.DOUBLE;
            }
            else
            {
                if (tokenCount <= 8)
                    sc = SimpleType.SimpleClassifier.BYTE;
                else if (tokenCount <= 16)
                    sc = SimpleType.SimpleClassifier.SHORT;
                else if (tokenCount <= 32)
                    sc = SimpleType.SimpleClassifier.INTEGER;
                else
                    sc = SimpleType.SimpleClassifier.DOUBLE;
            }

            _nodes.Add(new ValueNode("ByteLiteral", new SimpleType(sc, false), token.Value));
        }

        private void _visitComprehension(ASTNode node)
        {
            DataType elementType = new NoneType();

            // used in case of map comprehension
            DataType valueType = new NoneType();

            int sizeBack = 0;
            bool isKeyPair = false, isCondition = false, isList = ((TokenNode)node.Content[0]).Tok.Type == "[";

            var body = new List<ASTNode>();

            foreach (var item in ((ASTNode)node.Content[1]).Content)
            {
                if (item.Name == "expr")
                {
                    if (isCondition)
                    {
                        _visitExpr((ASTNode)item);

                        if (!new SimpleType(SimpleType.SimpleClassifier.BOOL).Coerce(_nodes.Last().Type))
                            throw new SemanticException("The condition of the comprehension must evaluate to a boolean", item.Position);

                        _nodes.Add(new ExprNode("Filter", new SimpleType(SimpleType.SimpleClassifier.BOOL)));
                        MergeBack();

                        sizeBack++;
                    }
                    else
                        body.Add((ASTNode)item);
                }
                else if (item.Name == "iterator")
                {
                    _table.AddScope();
                    _table.DescendScope();

                    _visitIterator((ASTNode)item, false);

                    sizeBack++;

                    foreach (var expr in body)
                    {
                        _visitExpr(expr);

                        // first expression
                        if (sizeBack == 1)
                            elementType = _nodes.Last().Type;
                        else if (isKeyPair && sizeBack == 2)
                            valueType = _nodes.Last().Type;

                        sizeBack++;
                    }
                }
                else if (item.Name == "TOKEN")
                {
                    if (((TokenNode)item).Tok.Type == ":")
                    {
                        if (isList)
                            throw new SemanticException("Unable to have key-value pairs in a list comprehension", item.Position);

                        isKeyPair = true;
                    }
                    else if (((TokenNode)item).Tok.Type == "WHEN")
                        isCondition = true;
                }
            }

            _table.AscendScope();

            if (isKeyPair)
                _nodes.Add(new ExprNode("DictComprehension", new DictType(elementType, valueType)));
            else if (isList)
                _nodes.Add(new ExprNode("ListComprehension", new ListType(elementType)));
            else
                _nodes.Add(new ExprNode("ArrayComprehension", new ArrayType(elementType, -1)));

            PushForward(sizeBack);
        }

        private void _visitLambda(ASTNode node, FunctionType ctx = null)
        {
            var args = new List<Parameter>();
            DataType rtType = new NoneType();
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
                        args = _generateArgsDecl((ASTNode)item, ctx?.Parameters);
                        break;
                    case "lambda_body":
                        if (args.Any(x => x.DataType is IncompleteType))
                        {
                            _nodes.Add(new IncompleteNode(node));
                            return;
                        }

                        _nodes.Add(new BlockNode("LambdaBody"));

                        // save and clear context
                        var vctx = _saveContext();
                        _clearContext();

                        // save and clear return context (slightly different from regular context)
                        var returnCtx = _returnContext;
                        _returnContext = ctx?.ReturnType;

                        for (int i = 0; i < args.Count; i++)
                        {
                            var arg = args[i];

                            if (arg.DefaultValue is IncompleteNode inode && _completeExprWithCtx(inode.AST, 
                                ctx == null ? arg.DataType : ctx.Parameters[i].DataType, out ITypeNode cDefVal))
                            {
                                arg.DefaultValue = cDefVal;
                            }
                        }

                        try
                        {
                            rtType = _visitFuncBody((ASTNode)item, args);
                        }
                        catch (SemanticException smex)
                        {
                            // if an error happens we still need to ascend out of our scope
                            _table.AscendScope();

                            throw smex;
                        }
                        finally
                        {
                            // make sure return context is restored (if it is expression level, it will not bubble high enough
                            // to negate the body return context and so this context must be restored in all cases)
                            _returnContext = returnCtx;

                            // restore context after visiting
                            _restoreContext(vctx);
                        }

                        if (ctx != null && !ctx.ReturnType.Coerce(rtType))
                            throw new SemanticException("Invalid return type for the given the context", item.Position);
                        break;
                }
            }

            var fType = new FunctionType(args, rtType, async, isBoxed: true); 

            _nodes.Add(new ExprNode("Lambda", fType));
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
                        throw new SemanticException("Invalid value for argument named " + fnType.Parameters[ndx].Name, item.Value.Position);

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

            _nodes.Add(new ExprNode("PartialFunction", new FunctionType(newParameters, fnType.ReturnType, fnType.Async, fnType.IsMethod, true)));

            // push forward root and args
            PushForward(removedArgs.Count + 1);
        }

        private void _visitSuperCall(ASTNode node)
        {
            if (_table.Lookup("this", out Symbol thisPtr))
            {
                var typeInterf = thisPtr.DataType.GetInterface();

                // 1 implement is bound type interface
                if (typeInterf.Implements.Count == 1)
                    throw new SemanticException("Unable to access parent where none exists", node.Position);
                // 2 implements is bound and other interfaces
                else if (typeInterf.Implements.Count == 2)
                    _nodes.Add(new ValueNode("Super", typeInterf.Implements.First().GetSuperInstance(), "()"));
                // 3+ implements is bound and multiple other interfaces
                else
                {
                    if (node.Content.Count == 3)
                        throw new SemanticException("Parent specification is required on types with more than one parent", node.Content[2].Position);

                    foreach (var item in node.Content.Skip(2))
                    {
                        switch (item.Name)
                        {
                            case "TOKEN":
                                if (((TokenNode)item).Tok.Type == "IDENTIFIER")
                                {
                                    var token = ((TokenNode)item).Tok;

                                    if (_table.Lookup(token.Value, out Symbol sym))
                                    {
                                        _nodes.Add(new IdentifierNode(token.Value, sym.DataType));
                                    }
                                    else
                                        throw new SemanticException($"Unable to find parent named `{token.Value}`", item.Position);
                                }
                                break;
                            case "static_get":
                                if (_nodes.Last().Type is PackageType pkg)
                                {
                                    var idNode = (TokenNode)((ASTNode)item).Content[1];

                                    if (pkg.Lookup(idNode.Tok.Value, out Symbol sym))
                                    {
                                        _nodes.Add(new IdentifierNode(sym.Name, sym.DataType));

                                        _nodes.Add(new ExprNode("StaticGet", sym.DataType));
                                        PushForward(2);
                                    }
                                    else
                                        throw new SemanticException($"The given package has no exported member: `{idNode.Tok.Value}", idNode.Position);
                                }
                                else
                                    throw new SemanticException("Static get can only be used on packages in a super argument", item.Position);
                                break;
                            case "generic_spec":
                                if (_nodes.Last().Type.Classify() == TypeClassifier.GENERIC)
                                {
                                    var genericType = _generateGeneric((GenericType)_nodes.Last().Type, (ASTNode)item);

                                    _nodes.Add(new ExprNode("CreateGeneric", genericType));
                                    PushForward();
                                }
                                else
                                    throw new SemanticException("Unable to apply generic specifier to type of " + _nodes.Last().Type.ToString(), 
                                        item.Position);
                                break;
                        }
                    }

                    if (_nodes.Last().Type.Classify() == TypeClassifier.INTERFACE)
                    {
                        var parent = ((InterfaceType)_nodes.Last().Type).GetInstance();

                        if (typeInterf.Implements.Any(x => x.Equals(parent)))
                        {
                            _nodes.Add(new ExprNode("Super", parent.GetSuperInstance()));
                            PushForward();
                        }
                        else
                            throw new SemanticException("The given interface is not a parent to the current type", node.Position);
                    }
                    else
                        throw new SemanticException($"Unable to use type of {_nodes.Last().Type.ToString()} as a parent", node.Position);
                }
            }
            else
                throw new SemanticException("Unable to use `super` outside of type method or constructor", node.Position);
        }
    }
}
