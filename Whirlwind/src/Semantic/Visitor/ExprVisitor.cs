using System.Collections.Generic;
using System.Linq;
using Whirlwind.Semantic.Constexpr;
using Whirlwind.Syntax;
using Whirlwind.Types;

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

                        if (_isVoidOrNull(_nodes.Last().Type))
                            throw new SemanticException("Unable to determine type of variable", exprVarDecl.Content[0].Position);

                        var copyType = _nodes.Last().Type.LValueCopy();
                        // clear out constancy
                        copyType.Constant = false;

                        _nodes.Add(new IdentifierNode(name, copyType));

                        try
                        {
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
                        }
                        finally
                        {
                            if (needsSubscope)
                                _table.AscendScope();
                        }
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
                            else if (item.Name == "select_extension")
                                _visitSelectExpr((ASTNode)item);
                            else if (item.Name == "then_extension" && op != "IS")
                                _visitThen((ASTNode)item, needsSubscope);
                        }

                        if (op == "IF")
                        {
                            var dt = _nodes.Last().Type;

                            // avoid null
                            if (_isVoidOrNull(dt))
                                dt = _nodes[_nodes.Count - 3].Type;

                            _nodes.Add(new ExprNode("InlineCompare", dt));
                            PushForward(3);

                            var content = ((ExprNode)_nodes.Last()).Nodes;

                            if (!new SimpleType(SimpleType.SimpleClassifier.BOOL).Coerce(content[1].Type))
                                throw new SemanticException("Comparison expression of inline comparison must evaluate to a boolean", node.Content[0].Position);

                            if (!content[0].Type.Coerce(content[2].Type) || !content[2].Type.Coerce(content[0].Type))
                                throw new SemanticException("Possible results of inline comparison must be the same type", ((ASTNode)subNode).Content.Last().Position);
                        }
                        else if (op == "IS")
                        {
                            ASTNode rOperand = (ASTNode)((ASTNode)subNode).Content[1];
                            bool createdScope = false;

                            DataType dt;
                            var typeNode = ((ASTNode)rOperand.Content.Last());
                            if (typeNode.Content[0] is TokenNode tk && tk.Tok.Type == "IDENTIFIER"
                                && _table.Lookup(tk.Tok.Value, out Symbol sym) && sym.DataType is CustomNewType)
                            {
                                dt = sym.DataType;
                            }
                            else
                                dt = _generateType(typeNode);

                            if (rOperand.Content.Count > 1)
                            {
                                var tkNode = ((TokenNode)rOperand.Content[0]);

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

                            var boolType = new SimpleType(SimpleType.SimpleClassifier.BOOL);

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
                            var mType = _nodes.Last().Type;

                            if (HasOverload(mType, "__:>__", out DataType rtType))
                            {
                                _thenExprType = rtType;

                                _visitExpr((ASTNode)extractExpr.Content[1]);

                                if (!mType.Coerce(_nodes.Last().Type))
                                    throw new SemanticException("Expression of `:>` operator must return the source type", 
                                        extractExpr.Content[1].Position);

                                _nodes.Add(new ExprNode("ExtractInto", mType));
                                PushForward(2);
                            }
                            else
                                throw new SemanticException("The `:>` operator is not defined on type of " + _nodes.Last().Type.ToString(), 
                                    extractExpr.Content[0].Position);
                        }
                        else if (op == ".")
                        {
                            var intType = new SimpleType(SimpleType.SimpleClassifier.INTEGER, true);

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
                            {
                                if (!(dt is StructType && (_getImpl(desired)?.Equals(dt) ?? false)))
                                    throw new SemanticException("Invalid type cast", subNode.Position);
                            }

                            // preserve category of cast expression
                            desired.Category = dt.Category;

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

            // check for type classes used without static get
            if (_nodes.Last().Type is CustomType)
                throw new SemanticException("Unable to use type definition as expression", node.Position);

            // apply constexpr optimization
            if (_constexprOptimizerEnabled && Evaluator.TryEval(_nodes.Last()))
                _nodes[_nodes.Count - 1] = Evaluator.Evaluate(_nodes.Last());

            // make r-value if necessary
            if (!LValueExpr(_nodes.Last()))
            {
                var nodeDt = _nodes.Last().Type.Copy();
                nodeDt.Category = ValueCategory.RValue;

                _nodes.Last().Type = nodeDt;
            }

            // box functions if necessary
            if (_nodes.Last().Type is FunctionType ft && !ft.IsBoxed)
                _nodes.Last().Type = ft.BoxedCopy();
        }

        private void _visitSelectExpr(ASTNode node)
        {
            DataType dt = new NoneType();
            DataType rootType = _nodes.Last().Type;

            if (!Hashable(rootType) || !(rootType is CustomType || rootType is TupleType))
                throw new SemanticException("Unable to perform select on a " + rootType.ToString(), node.Content[0].Position);

            int exprCount = 1;
            bool noDefaultCase = true;
            foreach (var item in node.Content)
            {
                if (item.Name == "inline_case")
                {
                    int exprs = 0;

                    _table.AddScope();
                    _table.DescendScope();

                    var caseNode = (ASTNode)item;

                    foreach (var elem in caseNode.Content)
                    {
                        if (elem.Name == "case_expr")
                        {
                            if (!_visitCaseExpr((ASTNode)elem, rootType, caseNode.Content.Count < 5))
                                throw new SemanticException("All conditions of select expression must be similar to the root type",
                                    elem.Position);

                            exprs++;
                        }
                    }

                    if (_isVoidOrNull(dt))
                        dt = _nodes.Last().Type;
                    else if (!dt.Coerce(_nodes.Last().Type))
                    {
                        if (_nodes.Last().Type.Coerce(dt))
                            dt = _nodes.Last().Type;
                        else
                        {
                            InterfaceType i1 = dt.GetInterface(), i2 = _nodes.Last().Type.GetInterface();

                            if (i1.Implements.Count == 0 || i2.Implements.Count == 0)
                                throw new SemanticException("All case results must be of similar types", 
                                    ((ASTNode)item).Content[((ASTNode)item).Content.Count - 2].Position);

                            var matches = i1.Implements.Where(x => i2.Implements.Any(y => y.Equals(x)));

                            if (matches.Count() > 0)
                                dt = matches.First();
                            else
                                throw new SemanticException("All case results must be of similar types",
                                        ((ASTNode)item).Content[((ASTNode)item).Content.Count - 2].Position);
                        }
                            
                    }

                    _nodes.Add(new ExprNode("Case", dt));
                    PushForward(exprs);

                    _table.AscendScope();

                    exprCount++;
                }
                else if (item.Name == "default_case")
                {
                    _visitExpr((ASTNode)((ASTNode)item).Content[3]);

                    if (_isVoidOrNull(dt))
                        dt = _nodes.Last().Type;
                    else if (!dt.Coerce(_nodes.Last().Type))
                    {
                        if (_nodes.Last().Type.Coerce(dt))
                            dt = _nodes.Last().Type;
                        else
                        {
                            InterfaceType i1 = dt.GetInterface(), i2 = _nodes.Last().Type.GetInterface();

                            if (i1.Implements.Count == 0 || i2.Implements.Count == 0)
                                throw new SemanticException("All case results must be of similar types", 
                                    ((ASTNode)item).Content.Last().Position);

                            var matches = i1.Implements.Where(x => i2.Implements.Contains(x));

                            if (matches.Count() > 0)
                                dt = matches.First();
                            else
                                throw new SemanticException("All case results must be of similar types", ((ASTNode)item).Content.Last().Position);
                        }            
                    }

                    _nodes.Add(new ExprNode("Default", dt));
                    PushForward();

                    noDefaultCase = false;
                    exprCount++;
                }
                else
                    continue;
            }

            // no default means need to check exhaustivity
            if (noDefaultCase)
            {
                bool testExhaustivity(ExprNode caseNode)
                {
                    if (caseNode.Name == "TypeClassPattern")
                    {
                        foreach (var elem in caseNode.Nodes)
                        {
                            if (elem.Name != "PatternSymbol" && elem.Name != "_")
                            {
                                if (elem is IdentifierNode idNode && idNode.Type is CustomInstance)
                                    continue;

                                return false;
                            }
                        }
                    }
                    else if (caseNode.Name == "TuplePattern")
                    {
                        foreach (var elem in caseNode.Nodes)
                        {
                            if (elem.Name != "PatternSymbol" && elem.Name != "_")
                                return false;
                        }
                    }
                    else
                        return false;

                    return true;
                }

                bool isExhaustive = false;
                if (rootType is TupleType tt)
                {                    
                    for (int i = 1; i < exprCount; i++)
                    {
                        if (_nodes[_nodes.Count - i] is ExprNode enode)
                        {
                            if (testExhaustivity((ExprNode)enode.Nodes[0]))
                            {
                                isExhaustive = true;
                                break;
                            }
                        }                           
                    }
                }
                else if (rootType is CustomInstance ci)
                {
                    // if it has a custom new type, then it is algebraic type
                    if (ci.Parent.Instances.First() is CustomNewType)
                    {
                        var instancesMatched = new List<string>();

                        for (int i = 1; i < exprCount; i++)
                        {
                            var item = ((ExprNode)_nodes[_nodes.Count - i]).Nodes[0];

                            if (item is ExprNode exprNode)
                            {
                                if (item.Type is CustomNewType cnt && (cnt.Values.Count == 0 || testExhaustivity(exprNode)))
                                {
                                    if (!instancesMatched.Contains(cnt.Name))
                                        instancesMatched.Add(cnt.Name);
                                }
                            }                                                      
                        }

                        isExhaustive = instancesMatched.Count == ci.Parent.Instances.Count;
                    }
                }

                if (!isExhaustive)
                    throw new SemanticException("All select expressions must be exhaustive", node.Position);
            }

            _nodes.Add(new ExprNode("SelectExpr", dt));
            PushForward(exprCount);
        }

        private bool _visitCaseExpr(ASTNode node, DataType rootType, bool allowPatternSymbols)
        {
            var caseContent = (ASTNode)node.Content[0];

            if (caseContent.Name == "expr")
            {
                _addContext(caseContent);
                _couldOwnerExist = false;

                _visitExpr(caseContent);
                _clearContext();               

                if (_nodes.Last() is IncompleteNode inode)
                {
                    _giveContext(inode, rootType);

                    _nodes[_nodes.Count - 2] = _nodes.Last();
                    _nodes.RemoveLast();
                }

                if (Evaluator.TryEval(_nodes.Last()))
                    _nodes[_nodes.Count - 1] = Evaluator.Evaluate(_nodes.Last());
                else
                    throw new SemanticException("Unable to use non-constant expression in case expression", caseContent.Position);

                if (!Hashable(_nodes.Last().Type))
                    throw new SemanticException("Types of case expressions must be integral or hashable", caseContent.Position);

                return rootType.Coerce(_nodes.Last().Type);
            }
            else
                return _visitPattern(caseContent, rootType, allowPatternSymbols);
        }

        private bool _visitPattern(ASTNode pattern, DataType rootType, bool allowPatternSymbols)
        {
            int patternElemCount = 0;
            DataType dt = new TupleType(new List<DataType>());

            foreach (var item in pattern.Content)
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
                        else if (rootType is CustomInstance ci)
                        {
                            var instanceMatches = ci.Parent.Instances
                                .Where(x => x is CustomNewType)
                                .Select(x => (CustomNewType)x)
                                .Where(x => x.Name == tk.Tok.Value);

                            if (instanceMatches.Count() > 0)
                            {
                                var match = instanceMatches.First();

                                _nodes.Add(new IdentifierNode(match.Name, match));
                                dt = match;
                            }
                            else
                                throw new SemanticException($"Undefined symbol: `{tk.Tok.Value}`", tk.Position);
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
                                if (allowPatternSymbols)
                                    _nodes.Add(new ValueNode("PatternSymbol", new NoneType(), itkn.Tok.Value));
                                else
                                    throw new SemanticException("Unable to use pattern variables in a case with multiple matches",
                                        anode.Position);

                                goto loopEnd;
                            }

                            iNode = anode.Content[0];
                        }

                        // no id found
                        _visitExpr((ASTNode)patternElem.Content.First());

                        if (Evaluator.TryEval(_nodes.Last()))
                            _nodes[_nodes.Count - 1] = Evaluator.Evaluate(_nodes.Last());
                        else
                            throw new SemanticException("Unable to use a non-constant expression in a pattern.", iNode.Position);

                        if (!Hashable(_nodes.Last().Type))
                            throw new SemanticException("Pattern values must be integral or hashable", iNode.Position);
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
                    else if (!rt.Types[i].Coerce(cNode.Type))
                        return false;
                }

                _nodes.Add(new ExprNode("TuplePattern", dt));
                PushForward(patternElemCount);
            }
            else if (dt is CustomNewType nt && rootType is CustomInstance rci && rci.Parent.Equals(nt.Parent))
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
                                rft.ReturnType, rft.Async, rft.IsMethod, isBoxed: true)));
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
                        if (!Mutable(_nodes.Last()))
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
                        if (!Mutable(_nodes.Last()))
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

                    if (rootType.Category == ValueCategory.RValue)
                        throw new SemanticException("Unable to reference an r-value", node.Content[0].Position);

                    treeName = "Indirect";
                    dt = new PointerType(rootType, false);
                    break;
                // dereference
                default:
                    if (rootType is PointerType pt)
                    {
                        treeName = op == "*?" ? "NullableDereference" : "Dereference";

                        dt = pt.DataType.LValueCopy();

                        if (_isVoidOrNull(dt))
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
