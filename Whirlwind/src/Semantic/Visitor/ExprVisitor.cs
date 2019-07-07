using Whirlwind.Parser;
using Whirlwind.Types;

using static Whirlwind.Semantic.Checker.Checker;

using System.Linq;
using System.Collections.Generic;

namespace Whirlwind.Semantic.Visitor
{
    partial class Visitor
    {
        DataType _thenExprType = new VoidType();

        private void _visitExpr(ASTNode node, bool needsSubscope=true)
        {
            foreach (var subNode in node.Content)
            {
                if (subNode.Name == "func_op")
                    _visitFuncOp((ASTNode)subNode);
                else if (subNode.Name == "expr_var")
                {
                    ASTNode exprVarDecl = (ASTNode)subNode;

                    string name = ((TokenNode)exprVarDecl.Content[0]).Tok.Value;
                    _visitFuncOp((ASTNode)exprVarDecl.Content[2]);

                    if (_isVoid(_nodes.Last().Type))
                        throw new SemanticException("Unable to determine type of variable", exprVarDecl.Content[0].Position);

                    var copyType = _nodes.Last().Type.ConstCopy();
                    // clear out constancy
                    copyType.Constant = false;

                    _nodes.Add(new IdentifierNode(name, copyType));

                    if (needsSubscope)
                    {
                        _table.AddScope();
                        _table.DescendScope();
                    }

                    if (!_table.AddSymbol(new Symbol(name, copyType)))
                    {
                        if (needsSubscope)
                            _table.AscendScope();

                        throw new SemanticException("Unable to redeclare symbol in scope", exprVarDecl.Content[0].Position);
                    }

                    _nodes.Add(new ExprNode("ExprVarDecl", new VoidType()));
                    PushForward(2);

                    _visitThen((ASTNode)exprVarDecl.Content[3], false);

                    if (needsSubscope)
                        _table.AscendScope();
                        
                }
                else if (subNode.Name == "expr_extension")
                {
                    string op = "";

                    foreach (var item in ((ASTNode)subNode).Content)
                    {
                        if (item.Name == "TOKEN" && op == "")
                            op = ((TokenNode)item).Tok.Type;
                        else if (op != ":>" && item.Name == "expr")
                            _visitExpr((ASTNode)item);
                        else if (item.Name == "case_extension")
                            _visitInlineCase((ASTNode)item);
                        else if (item.Name == "then_extension")
                            _visitThen((ASTNode)item, needsSubscope);
                    }

                    if (op == "IF")
                    {
                        _nodes.Add(new ExprNode("InlineCompare", _nodes.Last().Type));
                        PushForward(3);

                        var content = ((ExprNode)_nodes.Last()).Nodes;

                        if (!new SimpleType(SimpleType.SimpleClassifier.BOOL).Coerce(content[1].Type))
                            throw new SemanticException("Comparison expression of inline comparison must evaluate to a boolean", node.Content[0].Position);

                        if (!content[0].Type.Coerce(content[2].Type) || !content[2].Type.Coerce(content[0].Type))
                            throw new SemanticException("Possible results of inline comparison must be the same type", ((ASTNode)subNode).Content.Last().Position);
                    }
                    else if (op == "IS")
                    {
                        ASTNode typeNode = (ASTNode)((ASTNode)subNode).Content[1];
                        DataType dt;

                        if (typeNode.Content[0] is TokenNode tk && tk.Tok.Type == "IDENTIFIER"
                            && _table.Lookup(tk.Tok.Value, out Symbol sym) && sym.DataType is CustomNewType)
                        {
                            dt = sym.DataType;
                        }
                        else
                            dt = _generateType(typeNode);

                        _nodes.Add(new ValueNode("Type", dt));

                        _nodes.Add(new ExprNode("Is", new SimpleType(SimpleType.SimpleClassifier.BOOL)));

                        PushForward(2);
                    }
                    else if (op == ":>")
                    {
                        var extractExpr = (ASTNode)subNode;

                        if (HasOverload(_nodes.Last().Type, "__:>__", out DataType rtType))
                        {
                            _thenExprType = rtType;

                            _visitExpr((ASTNode)extractExpr.Content[1]);

                            _nodes.Add(new ExprNode("ExtractInto", _nodes.Last().Type));
                            PushForward(2);
                        }
                        else
                            throw new SemanticException("The `:>` operator is not defined on the given type", extractExpr.Content[0].Position);
                    }
                    else if (op == ".")
                    {
                        var intType = new SimpleType(SimpleType.SimpleClassifier.INTEGER);

                        if (!intType.Coerce(_nodes[_nodes.Count - 2].Type))
                        {
                            if (HasOverload(_nodes[_nodes.Count - 2].Type, "__..__", 
                                new ArgumentList(new List<DataType> { _nodes.Last().Type }), out DataType rtType))
                            {
                                _nodes.Add(new ExprNode("Range", rtType));
                                PushForward(2);

                                return;
                            }

                            throw new SemanticException("Range must be bounded by two integers", node.Content[0].Position);
                        }

                        if (!intType.Coerce(_nodes.Last().Type))
                            throw new SemanticException("Range must be bounded by two integers", ((ASTNode)subNode).Content[2].Position);

                        _nodes.Add(new ExprNode("Range", new ArrayType(intType, -1)));
                        PushForward(2);
                    }
                    else if (op == "AS")
                    {
                        DataType dt = new VoidType();
                        bool hasType = false;

                        foreach (var item in ((ASTNode)subNode).Content)
                        {
                            if (item is TokenNode tk && tk.Tok.Type == "IDENTIFIER")
                            {
                                if (_table.Lookup(tk.Tok.Value, out Symbol sym))
                                {
                                    if (sym.Modifiers.Contains(Modifier.CONSTEXPR))
                                        _nodes.Add(new ConstexprNode(sym.Name, sym.DataType, sym.Value));
                                    else
                                        _nodes.Add(new IdentifierNode(sym.Name, sym.DataType));

                                    if (sym.DataType is CustomInstance cinst)
                                    {
                                        if (!_table.Lookup(cinst.Parent.Name, out Symbol _))
                                            throw new SemanticException("Unable type class instances outside of type class's visible scope",
                                                node.Content[0].Position);
                                    }
                                }
                                else {
                                    throw new SemanticException($"Undefined Symbol: `{tk.Tok.Value}`", node.Position);
                                }
                            }
                            else if (item.Name == "trailer")
                            {
                                _visitTrailer((ASTNode)item);

                                if (new[] { "GetTupleMember", "Subscript", "Slice", "InitList", "Call", "CallAsync", "CallConstructor",
                                    "InitStruct", "InitTCConstructor" }.Contains(_nodes.Last().Name))
                                {
                                    throw new SemanticException("Operation not valid in right side of `as` operator", item.Position);
                                }
                            }
                            else if (item.Name == "types")
                            {
                                dt = _generateType((ASTNode)item);
                                hasType = true;
                            }
                                
                        }

                        if (!hasType)
                            dt = _nodes.Last().Type;

                        if (dt.Coerce(_nodes[_nodes.Count - 2].Type))
                        {
                            _nodes.Add(new ExprNode("As", dt));
                            PushForward(2);
                        }
                        else
                            throw new SemanticException("Unable to perform explicit coercion between the two types", subNode.Position);
                    }
                }
            }
        }

        private void _visitInlineCase(ASTNode node)
        {
            DataType dt = new VoidType();
            DataType rootType = _nodes.Last().Type;

            int caseCount = 2;
            foreach (var item in node.Content)
            {
                if (item.Name == "inline_case")
                {
                    int exprs = 0;
                    bool checkedExpr = true;

                    foreach (var elem in ((ASTNode)item).Content)
                    {
                        if (elem.Name == "expr")
                        {
                            _visitExpr((ASTNode)elem);

                            if (checkedExpr && !rootType.Coerce(_nodes.Last().Type))
                                throw new SemanticException("All conditions of case expression must be similar to the root type", 
                                    elem.Position);

                            exprs++;
                        }
                        else if (elem.Name == "TOKEN" && ((TokenNode)elem).Tok.Value == "=>")
                            checkedExpr = false;
                    }

                    if (_isVoid(dt))
                        dt = _nodes.Last().Type;
                    else if (!dt.Coerce(_nodes.Last().Type))
                    {
                        if (_nodes.Last().Type.Coerce(dt))
                            dt = _nodes.Last().Type;
                        else
                        {
                            InterfaceType i1 = dt.GetInterface(), i2 = _nodes.Last().Type.GetInterface();

                            if (i1.Implements.Count == 0 || i2.Implements.Count == 0)
                                throw new SemanticException("All values in a collection must be the same type", 
                                    ((ASTNode)item).Content[((ASTNode)item).Content.Count - 2].Position);

                            var matches = i1.Implements.Where(x => i2.Implements.Any(y => y.Equals(x)));

                            if (matches.Count() > 0)
                                dt = matches.First();
                            else
                                throw new SemanticException("All case types must match",
                                        ((ASTNode)item).Content[((ASTNode)item).Content.Count - 2].Position);
                        }
                            
                    }

                    _nodes.Add(new ExprNode("Case", dt));
                    PushForward(exprs);

                    caseCount++;
                }
                else if (item.Name == "default_case")
                {
                    _visitExpr((ASTNode)((ASTNode)item).Content[2]);

                    if (_isVoid(dt))
                        dt = _nodes.Last().Type;
                    else if (!dt.Coerce(_nodes.Last().Type))
                    {
                        if (_nodes.Last().Type.Coerce(dt))
                            dt = _nodes.Last().Type;
                        else
                        {
                            InterfaceType i1 = dt.GetInterface(), i2 = _nodes.Last().Type.GetInterface();

                            if (i1.Implements.Count == 0 || i2.Implements.Count == 0)
                                throw new SemanticException("All values in a collection must be the same type", 
                                    ((ASTNode)item).Content.Last().Position);

                            var matches = i1.Implements.Where(x => i2.Implements.Contains(x));

                            if (matches.Count() > 0)
                                dt = matches.First();
                            else
                                throw new SemanticException("All case types must match", ((ASTNode)item).Content.Last().Position);
                        }            
                    }

                    _nodes.Add(new ExprNode("Default", dt));
                    PushForward();
                }
                else
                    continue;
            }

            _nodes.Add(new ExprNode("InlineCaseExpr", dt));
            PushForward(caseCount);
        }

        private void _visitThen(ASTNode node, bool needsSubscope)
        {
            _thenExprType = _nodes.Last().Type;

            _visitExpr((ASTNode)node.Content[1], needsSubscope);

            _nodes.Add(new ExprNode("Then", _nodes.Last().Type));
            PushForward(2);
        }

        private void _visitFuncOp(ASTNode node)
        {
            if (node.Content.Count > 1)
            {
                int opPos = 1;
                string treeName;

                _visitLogical((ASTNode)node.Content[0]);
                DataType rootType = _nodes.Last().Type;

                while (opPos < node.Content.Count)
                {
                    treeName = ((TokenNode)node.Content[opPos]).Tok.Type == ">>=" ? "Bind" : "Compose";

                    _visitLogical((ASTNode)node.Content[opPos + 1]);

                    if (treeName == "Bind")
                    {
                        if (HasOverload(rootType, "__>>=__", new ArgumentList(new List<DataType> { _nodes.Last().Type }), out DataType rtType))
                            _nodes.Add(new ExprNode("Bind", rtType));
                        else
                            throw new SemanticException("The `>>=` operator is not defined on the given type", node.Content[opPos].Position);
                    }
                    else
                    {
                        if (rootType is FunctionType rft && _nodes.Last().Type is FunctionType oft)
                        {
                            if (rft.Parameters.Skip(1).Select(x => x.Name).Any(x => oft.Parameters.Any(y => x == y.Name)))
                                throw new SemanticException("Composition cannot result in duplicate parameters", node.Content[opPos].Position);

                            _nodes.Add(new ExprNode("Compose", new FunctionType(rft.Parameters.Skip(1).Concat(oft.Parameters).ToList(),
                                rft.ReturnType, rft.Async)));
                        }
                        else if (HasOverload(rootType, "__~*__", new ArgumentList(new List<DataType> { _nodes.Last().Type}), 
                            out DataType rtType))
                        {
                            _nodes.Add(new ExprNode("Compose", rtType));
                        }
                        else
                            throw new SemanticException("The `~*` operator not defined on the given types", node.Content[opPos].Position);
                    }

                    PushForward(2);

                    rootType = _nodes.Last().Type;
                    opPos += 2;
                }
            }
            else
                _visitLogical((ASTNode)node.Content[0]);
        }

        private void _visitLogical(ASTNode node)
        {
            string op = "";
            bool hitFirst = false;
            DataType rootType = new VoidType();

            foreach (var subNode in node.Content)
            {
                if (subNode.Name == "TOKEN")
                {
                    string tokenType = ((TokenNode)subNode).Tok.Type;

                    if (tokenType != op)
                    {
                        _nodes.Add(new ExprNode(_getOpTreeName(tokenType), rootType));
                        PushForward();

                        op = tokenType;

                        if (!hitFirst)
                            hitFirst = true;
                    }

                    continue;
                }
                else if (subNode.Name == "comparison")
                    _visitComparison((ASTNode)subNode);
                else
                    _visitLogical((ASTNode)subNode);

                if (hitFirst)
                {
                    CheckOperand(ref rootType, _nodes.Last().Type, op, subNode.Position);
                    MergeBack();

                    _nodes[_nodes.Count - 1].Type = rootType;
                }
                else
                    rootType = _nodes.Last().Type;
            }
        }

        private void _visitComparison(ASTNode node)
        {
            string op = "";
            bool hitFirst = false;
            DataType rootType = new VoidType();

            foreach (var subNode in node.Content)
            {
                if (subNode.Name == "comparison_op")
                {
                    string tokenType = ((TokenNode)((ASTNode)subNode).Content[0]).Tok.Type;

                    if (tokenType != op)
                    {
                        _nodes.Add(new ExprNode(_getOpTreeName(tokenType), rootType));
                        PushForward();

                        op = tokenType;

                        if (!hitFirst)
                            hitFirst = true;
                    }

                    continue;
                }
                // not
                else
                {
                    ASTNode notNode = (ASTNode)subNode;
                    if (notNode.Content.Count == 2)
                    {
                        _visitShift((ASTNode)notNode.Content[1]);

                        if (new SimpleType(SimpleType.SimpleClassifier.BOOL).Coerce(_nodes.Last().Type))
                        {
                            _nodes.Add(new ExprNode("Not", new SimpleType(SimpleType.SimpleClassifier.BOOL)));
                            PushForward();
                        }
                        else if (_nodes.Last().Type.Classify() == TypeClassifier.SIMPLE)
                        {
                            _nodes.Add(new ExprNode("Not", _nodes.Last().Type));
                            PushForward();
                        }
                        else if (HasOverload(_nodes.Last().Type, "__!__", out DataType returnType))
                        {
                            _nodes.Add(new ExprNode("Not", returnType));
                            PushForward();
                        }
                        else
                            throw new SemanticException("The not operator is not valid for the given type", notNode.Content[1].Position);
                    }
                    else
                        _visitShift((ASTNode)notNode.Content[0]);
                }
                    

                if (hitFirst)
                {
                    CheckOperand(ref rootType, _nodes.Last().Type, op, subNode.Position);
                    MergeBack();

                    _nodes[_nodes.Count - 1].Type = rootType;
                }
                else
                    rootType = _nodes.Last().Type;
            }
        }

        private void _visitShift(ASTNode node)
        {
            string op = "";
            bool hitFirst = false;
            DataType rootType = new VoidType();

            foreach (var subNode in node.Content)
            {

                if (subNode.Name == "shift_op")
                {
                    string tempOp = "";

                    tempOp = string.Join("", ((ASTNode)subNode).Content.Select(x => ((TokenNode)x).Tok.Type));

                    if (tempOp != op)
                    {
                        _nodes.Add(new ExprNode(_getOpTreeName(tempOp), rootType));
                        PushForward();

                        op = tempOp;

                        if (!hitFirst)
                            hitFirst = true;
                    }
                }
                else
                {
                    _visitArithmetic((ASTNode)subNode);

                    if (hitFirst)
                    {
                        CheckOperand(ref rootType, _nodes.Last().Type, op, subNode.Position);
                        MergeBack();

                        _nodes[_nodes.Count - 1].Type = rootType;
                    }
                    else
                        rootType = _nodes.Last().Type;
                }
            }
        }

        private void _visitArithmetic(ASTNode node)
        {
            string op = "";
            bool hitFirst = false;
            DataType rootType = new VoidType();

            foreach (var subNode in node.Content)
            {
                if (subNode.Name == "TOKEN")
                {
                    string tokenType = ((TokenNode)subNode).Tok.Type;
                    
                    if (tokenType != op)
                    {
                        _nodes.Add(new ExprNode(_getOpTreeName(tokenType), rootType));
                        PushForward();

                        op = tokenType;

                        if (!hitFirst)
                            hitFirst = true;
                    }

                    continue;
                }
                else if (subNode.Name == "unary_atom")
                    _visitUnaryAtom((ASTNode)subNode);
                else
                    _visitArithmetic((ASTNode)subNode);

                if (hitFirst)
                {
                    CheckOperand(ref rootType, _nodes.Last().Type, op, subNode.Position);
                    MergeBack();

                    _nodes[_nodes.Count - 1].Type = rootType;
                }                
                else
                    rootType = _nodes.Last().Type;
            }
        }

        private string _getOpTreeName(string tokenType)
        {
            switch (tokenType)
            {
                case "+":
                    return "Add";
                case "-":
                    return "Sub";
                case "*":
                    return "Mul";
                case "/":
                    return "Div";
                case "~/":
                    return "Floordiv";
                case "%":
                    return "Mod";
                case "^":
                    return "Pow";
                case ">":
                    return "Gt";
                case "<":
                    return "Lt";
                case ">=":
                    return "GtEq";
                case "<=":
                    return "LtEq";
                case "==":
                    return "Eq";
                case "!=":
                    return "Neq";
                case "AND":
                    return "And";
                case "OR":
                    return "Or";
                case "XOR":
                    return "Xor";
                case ">>":
                    return "RShift";
                // "<<"
                default:
                    return "LShift";
            }
        }

        private void _visitUnaryAtom(ASTNode node)
        {
            bool postfix = false;
            string op = "";

            foreach (var subNode in node.Content)
            {
                if (subNode.Name == "atom")
                {
                    _visitAtom((ASTNode)subNode);
                    if (op == "")
                        postfix = true;
                }

                else if (subNode.Name == "TOKEN")
                    op += ((TokenNode)subNode).Tok.Type;
            }

            if (op == "")
                return;

            string treeName;

            DataType rootType = _nodes.Last().Type;
            DataType dt;


            switch (op)
            {
                case "++":
                    if (Numeric(rootType) || rootType.Classify() == TypeClassifier.POINTER)
                    {
                        if (!Modifiable(_nodes.Last()))
                            throw new SemanticException("Unable to mutate constant value", node.Content[postfix ? 1 : 0].Position);

                        treeName = (postfix ? "Postfix" : "Prefix") + "Increment";
                        dt = rootType;
                    }
                    else
                        throw new SemanticException("Increment operator is not valid on non-numeric types", node.Content[postfix ? 1 : 0].Position);
                    break;
                case "--":
                    if (Numeric(rootType) || rootType.Classify() == TypeClassifier.POINTER)
                    {
                        if (!Modifiable(_nodes.Last()))
                            throw new SemanticException("Unable to mutate constant value", node.Content[postfix ? 1 : 0].Position);

                        treeName = (postfix ? "Postfix" : "Prefix") + "Decrement";
                        dt = rootType;
                    }
                    else
                        throw new SemanticException("Decrement operator is not valid on non-numeric types", node.Content[postfix ? 1 : 0].Position);
                    break;
                case "-":
                    treeName = "ChangeSign";
                    if (Numeric(rootType))
                    {
                        // only simple types are numeric - remove unsigned
                        dt = new SimpleType(((SimpleType)rootType).Type);
                    }
                    else if (HasOverload(rootType, "__-__", out DataType newDt))
                    {
                        _nodes.Add(new ExprNode("ChangeSign", newDt));
                        // push root type
                        PushForward();
                        return;
                    }
                    else
                        throw new SemanticException("Unable to change sign of non-numeric type", node.Content[0].Position);
                    break;
                case "~":
                    treeName = "Complement";

                    if (rootType.Classify() == TypeClassifier.SIMPLE)
                        dt = rootType;
                    else if (HasOverload(rootType, "__~__", out DataType newDt))
                    {
                        _nodes.Add(new ExprNode("Complement", newDt));
                        // push root type
                        PushForward();
                        return;

                    }
                    else
                        throw new SemanticException("The complement operator is not valid for the given type", node.Content[0].Position);
                    break;
                case "&":
                    if (new[] {
                        TypeClassifier.STRUCT, TypeClassifier.INTERFACE, TypeClassifier.GENERIC,
                        TypeClassifier.FUNCTION, TypeClassifier.FUNCTION_GROUP
                    }.Contains(rootType.Classify()))
                        throw new SemanticException("The given object is not able to referenced", node.Content[0].Position);
                    treeName = "Indirect";
                    if (rootType.Classify() == TypeClassifier.POINTER)
                    {
                        var pType = (PointerType)rootType;
                        dt = new PointerType(pType.DataType, pType.Pointers + 1);
                    }
                    else
                        dt = new PointerType(rootType, 1);             
                    break;
                case "REF":
                    if (rootType.Classify() == TypeClassifier.REFERENCE)
                        throw new SemanticException("Unable to create a double reference", node.Content[0].Position);

                    treeName = "Reference";
                    dt = new ReferenceType(rootType);
                    break;
                // dereference
                default:
                    if (rootType.Classify() == TypeClassifier.POINTER)
                    {
                        int pointerCount = ((PointerType)rootType).Pointers;
                        if (op.Length > pointerCount)
                            throw new SemanticException("Unable to dereference a non-pointer", node.Content[op.Length - pointerCount - 1].Position);

                        treeName = "Dereference";
                        dt = op.Length == pointerCount ? ((PointerType)rootType).DataType : new PointerType(((PointerType)rootType).DataType, pointerCount - op.Length);

                        if (_isVoid(dt))
                            throw new SemanticException("Unable to dereference a void pointer", node.Content[node.Content.Count - 1].Position);
                    }
                    else
                        throw new SemanticException("Unable to dereference a non-pointer", node.Content[op.Length - 1].Position);
                    break;
            }

            _nodes.Add(new ExprNode(treeName, dt));
            PushForward();
        }
    }
}
