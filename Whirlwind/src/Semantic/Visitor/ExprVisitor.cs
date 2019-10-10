using System.Linq;
using System.Collections.Generic;

using Whirlwind.Syntax;
using Whirlwind.Types;
using Whirlwind.Semantic.Constexpr;

using static Whirlwind.Semantic.Checker.Checker;

namespace Whirlwind.Semantic.Visitor
{
    partial class Visitor
    {
        DataType _thenExprType = new NoneType();

        private void _visitExpr(ASTNode node, bool needsSubscope=true)
        {
            bool isExprStmt = _isExprStmt;
            _isExprStmt = false;

            try
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

                        _nodes.Add(new ExprNode("ExprVarDecl", new NoneType()));
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
                            else if (item.Name == "then_extension" && op != "IS")
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
                            ASTNode typeId = (ASTNode)((ASTNode)subNode).Content[1];
                            DataType dt;

                            var typeNode = ((ASTNode)typeId.Content.Last());
                            if (typeNode.Content[0] is TokenNode tk && tk.Tok.Type == "IDENTIFIER"
                                && _table.Lookup(tk.Tok.Value, out Symbol sym) && sym.DataType is CustomNewType)
                            {
                                dt = sym.DataType;
                            }
                            else
                                dt = _generateType(typeNode);

                            var boolType = new SimpleType(SimpleType.SimpleClassifier.BOOL);
                            bool createdScope = false;

                            if (typeId.Content.Count > 1)
                            {
                                var tkNode = ((TokenNode)typeId.Content[0]);

                                if (needsSubscope)
                                {
                                    _table.AddScope();
                                    _table.DescendScope();

                                    createdScope = true;
                                }

                                if (!_table.AddSymbol(new Symbol(tkNode.Tok.Value, dt)))
                                {
                                    if (needsSubscope)
                                        _table.AscendScope();

                                    throw new SemanticException("Unable to redeclare symbol in scope", tkNode.Position);
                                }

                                _nodes.Add(new IdentifierNode(tkNode.Tok.Value, dt));
                            }
                            else
                                _nodes.Add(new ValueNode("Type", dt));

                            _nodes.Add(new ExprNode("Is", boolType));

                            PushForward(2);

                            try
                            {
                                if (((ASTNode)subNode).Content.Last() is ASTNode anode && anode.Name == "then_extension")
                                {
                                    _thenExprType = boolType;

                                    _visitThen(anode, false);
                                }
                            }
                            finally
                            {
                                if (createdScope)
                                    _table.AscendScope();
                            }
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
                                throw new SemanticException("The `:>` operator is not defined on type of " + _nodes.Last().Type.ToString(), 
                                    extractExpr.Content[0].Position);
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
                            DataType dt = _nodes.Last().Type;
                            var typesNode = (ASTNode)((ASTNode)subNode).Content[1];

                            if (dt is CustomInstance cin && typesNode.Content.Count == 1
                                && typesNode.Content[0] is TokenNode tkn
                                && tkn.Tok.Type == "IDENTIFIER")
                            {
                                if (cin.Parent.GetInstanceByName(tkn.Tok.Value, out CustomNewType cnt))
                                {
                                    _nodes.Add(new ExprNode("TypeCast", cnt));
                                    PushForward();
                                    continue;
                                }
                            }

                            _isTypeCast = true;
                            DataType desired;

                            try
                            {
                                desired = _generateType(typesNode);
                            }
                            finally
                            {
                                _isTypeCast = false;
                            }
                            

                            if (!TypeCast(dt, desired))
                                throw new SemanticException("Invalid type cast", subNode.Position);

                            _nodes.Add(new ExprNode("TypeCast", desired));
                            PushForward();
                        }
                    }
                }
            }
            catch (SemanticContextException)
            {
                _nodes.Add(new IncompleteNode(node));
                _didTypeClassCtxInferFail = true;
            }
            catch (SemanticSelfIncompleteException)
            {
                throw new SemanticException("Use of incomplete self-referential type in expression", node.Position);
            }

            // don't worry with other stuff if incomplete
            if (_nodes.Last() is IncompleteNode)
                return;

            // check for erroneous void types
            if (_nodes.Last().Type is NoneType && !isExprStmt)
                throw new SemanticException("Unable to use an expression that returns no value", node.Position);

            // apply constexpr optimization
            if (_constexprOptimizerEnabled && Evaluator.TryEval(_nodes.Last()))
                _nodes[_nodes.Count - 1] = Evaluator.Evaluate(_nodes.Last());
        }

        private void _visitInlineCase(ASTNode node)
        {
            DataType dt = new NoneType();
            DataType rootType = _nodes.Last().Type;

            int caseCount = 2;
            foreach (var item in node.Content)
            {
                if (item.Name == "inline_case")
                {
                    int exprs = 0;

                    _table.AddScope();
                    _table.DescendScope();

                    foreach (var elem in ((ASTNode)item).Content)
                    {
                        if (elem.Name == "case_expr")
                        {
                            if (!_visitCaseExpr((ASTNode)elem, rootType))
                                throw new SemanticException("All conditions of case expression must be similar to the root type",
                                    elem.Position);

                            exprs++;
                        }
                        else if (elem.Name == "expr")
                        {
                            _visitExpr((ASTNode)elem, false);

                            exprs++;
                        }
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

                    _table.AscendScope();
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

        private bool _visitCaseExpr(ASTNode node, DataType rootType)
        {
            var caseContent = (ASTNode)node.Content[0];

            if (caseContent.Name == "expr")
            {
                _visitExpr(caseContent);
                return _nodes.Last().Type.Coerce(rootType);
            }              
            else
            {
                int patternElemCount = 0;
                DataType dt = new TupleType(new List<DataType>());

                foreach (var item in caseContent.Content)
                {
                    if (item is TokenNode tk)
                    {
                        if (tk.Tok.Type == "IDENTIFIER")
                        {
                            if (_table.Lookup(tk.Tok.Value, out Symbol sym))
                            {
                                if (sym.DataType is CustomType || sym.DataType is CustomNewType || sym.DataType is PackageType)
                                {
                                    _nodes.Add(new IdentifierNode(sym.Name, sym.DataType));
                                    dt = sym.DataType;
                                }                                    
                                else
                                    throw new SemanticException("Unable to pattern match over type of " + sym.DataType.ToString(), tk.Position);
                            }
                            else if (rootType is CustomNewType cn && tk.Tok.Value == cn.Name)
                            {
                                _nodes.Add(new IdentifierNode(cn.Name, cn));
                                dt = cn;
                            }
                            else
                                throw new SemanticException($"Undefined symbol: `{tk.Tok.Value}`", tk.Position);
                        }
                    }
                    else if (item.Name == "static_get")
                    {
                        var name = (TokenNode)((ASTNode)item).Content[1];

                        var sym = _getStaticMember(dt, name.Tok.Value, ((ASTNode)item).Content[0].Position, name.Position);

                        if (sym.DataType is CustomType || sym.DataType is CustomNewType || sym.DataType is PackageType)
                        {
                            _nodes.Add(new IdentifierNode(sym.Name, sym.DataType));

                            _nodes.Add(new ExprNode("StaticGet", sym.DataType));

                            PushForward(2);

                            dt = sym.DataType;
                        }
                        else
                            throw new SemanticException("Unable to pattern match over type of " + sym.DataType.ToString(), name.Position);
                    }
                    else if (item.Name == "pattern_elem")
                    {
                        var patternElem = (ASTNode)item;
                        
                        // _ case
                        if (patternElem.Content.First() is TokenNode)
                            _nodes.Add(new ValueNode("_", new NoneType()));
                        // expr or identifier
                        else
                        {
                            INode iNode = patternElem.Content[0];

                            while (iNode is ASTNode anode && anode.Content.Count == 1)
                            {
                                if (anode.Content[0] is TokenNode itkn && itkn.Tok.Type == "IDENTIFIER")
                                {
                                    if (_table.Lookup(itkn.Tok.Value, out Symbol sym))
                                        _nodes.Add(new IdentifierNode(sym.Name, sym.DataType));
                                    else
                                        _nodes.Add(new ValueNode("PatternSymbol", new NoneType(), itkn.Tok.Value));

                                    goto loopEnd;
                                }

                                iNode = anode.Content[0];
                            }

                            // no id found
                            _visitExpr((ASTNode)patternElem.Content.First());
                        }

                        loopEnd:

                        patternElemCount++;

                        if (dt is TupleType ttc)
                            ttc.Types.Add(_nodes.Last().Type);
                        else if (dt is CustomNewType ntc)
                            ntc.Values.Add(_nodes.Last().Type);
                    }
                }

                if (dt is TupleType tt && rootType is TupleType rt)
                {
                    for (int i = 0; i < patternElemCount; i++)
                    {
                        var cNode = _nodes[_nodes.Count - patternElemCount + i];

                        if (cNode is ValueNode vn)
                        {
                            if (vn.Name == "PatternSymbol")
                                _table.AddSymbol(new Symbol(vn.Value, rt.Types[i]));
                        }
                        else if (!cNode.Type.Coerce(tt.Types[i]))
                            return false;
                    }

                    _nodes.Add(new ExprNode("TuplePattern", dt));
                    PushForward(patternElemCount);
                }
                else if (dt is CustomNewType nt && rootType is CustomInstance)
                {
                    for (int i = 0; i < patternElemCount; i++)
                    {
                        var cNode = _nodes[_nodes.Count - patternElemCount + i];

                        if (cNode is ValueNode vn)
                        {
                            if (vn.Name == "PatternSymbol")
                                _table.AddSymbol(new Symbol(vn.Value, nt.Values[i]));
                        }
                        else if (!cNode.Type.Coerce(nt.Values[i]))
                            return false;
                    }

                    _nodes.Add(new ExprNode("TypeClassPattern", dt));
                    PushForward(patternElemCount + 1);
                }
                else
                    return false;               

                return true;
            }
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
                            throw new SemanticException("The `>>=` operator is not defined on type of " + rootType.ToString(), 
                                node.Content[opPos].Position);
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
                            throw new SemanticException($"The `~*` operator not defined between types of {rootType.ToString()} and {_nodes.Last().Type.ToString()}", 
                                node.Content[opPos].Position);
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
            DataType rootType = new NoneType();

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
            DataType rootType = new NoneType();

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
            DataType rootType = new NoneType();

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
            DataType rootType = new NoneType();

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
                case "~^":
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
                        throw new SemanticException("Increment operator is not valid on type of " + rootType.ToString(), 
                            node.Content[postfix ? 1 : 0].Position);
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
                        throw new SemanticException("Decrement operator is not valid on type of " + rootType.ToString(), 
                            node.Content[postfix ? 1 : 0].Position);
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
                        throw new SemanticException("Unable to change sign of a type of " + rootType.ToString(), node.Content[0].Position);
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
                        throw new SemanticException("The complement operator is not valid on type of " + rootType.ToString(), 
                            node.Content[0].Position);
                    break;
                case "&":
                    if (new[] {
                        TypeClassifier.STRUCT, TypeClassifier.INTERFACE, TypeClassifier.GENERIC,
                        TypeClassifier.FUNCTION, TypeClassifier.FUNCTION_GROUP
                    }.Contains(rootType.Classify()))
                        throw new SemanticException("Unable to reference type of " + rootType.ToString(), node.Content[0].Position);
                    treeName = "Indirect";
                    dt = new PointerType(rootType, false);
                    break;
                // dereference
                default:
                    if (rootType is PointerType pt)
                    {
                        treeName = op == "*?" ? "NullableDereference" : "Dereference";
                        dt = pt.DataType;

                        if (_isVoid(dt))
                            throw new SemanticException("Unable to dereference a pointer to none", node.Content[node.Content.Count - 1].Position);
                    }
                    else
                        throw new SemanticException("Unable to dereference a type of " + rootType.ToString(), node.Content[op.Length - 1].Position);
                    break;
            }

            _nodes.Add(new ExprNode(treeName, dt));
            PushForward();
        }
    }
}
