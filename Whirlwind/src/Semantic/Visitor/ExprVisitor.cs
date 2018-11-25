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
        private void _visitExpr(ASTNode node)
        {
            foreach (var subNode in node.Content)
            {
                if (subNode.Name == "or")
                    _visitLogical((ASTNode)subNode);
                else if (subNode.Name == "expr_extension")
                {
                    string op = "";

                    foreach (var item in ((ASTNode)subNode).Content)
                    {
                        if (item.Name == "TOKEN" && op == "")
                            op = ((TokenNode)item).Tok.Type;
                        else if (item.Name == "or")
                            _visitLogical((ASTNode)item);
                    }

                    if (op == "?")
                    {
                        _nodes.Add(new ExprNode("InlineCompare", _nodes.Last().Type));
                        PushForward(3);

                        var content = ((ExprNode)_nodes.Last()).Nodes;

                        if (!new SimpleType(SimpleType.DataType.BOOL).Coerce(content[0].Type))
                            throw new SemanticException("Comparison expression of inline comparison must evaluate to a boolean", node.Content[0].Position);

                        if (!content[1].Type.Coerce(content[2].Type) || !content[2].Type.Coerce(content[1].Type))
                            throw new SemanticException("Possible results of inline comparison must be the same type", ((ASTNode)subNode).Content.Last().Position);
                    }
                    else
                    {
                        _nodes.Add(new ExprNode("NullCoalesce", _nodes.Last().Type));
                        PushForward(2);

                        var content = ((ExprNode)_nodes.Last()).Nodes;

                        if (!content[0].Type.Coerce(content[1].Type) || !content[1].Type.Coerce(content[0].Type))
                            throw new SemanticException("Base value and coalesced value of null coalescion must be the same type", ((ASTNode)subNode).Content.Last().Position);
                    }
                }
            }
        }

        private void _visitLogical(ASTNode node)
        {
            string op = "";
            bool hitFirst = false;
            IDataType rootType = new SimpleType();

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
            IDataType rootType = new SimpleType();

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

                        if (new SimpleType(SimpleType.DataType.BOOL).Coerce(_nodes.Last().Type))
                        {
                            _nodes.Add(new ExprNode("Not", new SimpleType(SimpleType.DataType.BOOL)));
                            PushForward();
                        }
                        else if (_nodes.Last().Type.Classify() == TypeClassifier.SIMPLE)
                        {
                            _nodes.Add(new ExprNode("Not", _nodes.Last().Type));
                            PushForward();
                        }
                        else if (HasOverload(_nodes.Last().Type, "__not__", out IDataType returnType))
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
            IDataType rootType = new SimpleType();

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
            IDataType rootType = new SimpleType();

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

            IDataType rootType = _nodes.Last().Type;
            IDataType dt;


            switch (op)
            {
                case "++":
                    if (Modifiable(_nodes.Last()) && (Numeric(rootType) || rootType.Classify() == TypeClassifier.POINTER))
                    {
                        treeName = (postfix ? "Postfix" : "Prefix") + "Increment";
                        dt = rootType;
                    }
                    else
                        throw new SemanticException("Increment operator is not valid on non-numeric types", node.Content[postfix ? 2 : 0].Position);
                    break;
                case "--":
                    if (Modifiable(_nodes.Last()) && (Numeric(rootType) || rootType.Classify() == TypeClassifier.POINTER))
                    {
                        treeName = (postfix ? "Postfix" : "Prefix") + "Decrement";
                        dt = rootType;
                    }
                    else
                        throw new SemanticException("Decrement operator is not valid on non-numeric types", node.Content[postfix ? 2 : 0].Position);
                    break;
                case "-":
                    treeName = "ChangeSign";
                    if (Numeric(rootType))
                    {
                        // only simple types are numeric - remove unsigned
                        dt = new SimpleType(((SimpleType)rootType).Type);
                    }
                    else if (HasOverload(rootType, "__neg__", out IDataType newDt))
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
                    else if (HasOverload(rootType, "__comp__", out IDataType newDt))
                    {
                        _nodes.Add(new ExprNode("Complement", newDt));
                        // push root type
                        PushForward();
                        return;

                    }
                    else
                        throw new SemanticException("The complement operator is not valid for the given type", node.Content[0].Position);
                    break;
                case "&VOL":
                case "&":
                    if (new[] {
                        TypeClassifier.STRUCT, TypeClassifier.INTERFACE, TypeClassifier.OBJECT, TypeClassifier.TEMPLATE, TypeClassifier.FUNCTION
                    }.Contains(rootType.Classify()))
                        throw new SemanticException("The given object is not reference able", node.Content[0].Position);
                    treeName = op == "&" ? "Reference" : "VolatileReference";
                    if (rootType.Classify() == TypeClassifier.POINTER)
                    {
                        ((PointerType)rootType).Pointers++;
                        dt = rootType;
                    }
                    else
                        dt = new PointerType(rootType, 1);             
                    break;
                // dereference
                default:
                    if (rootType.Classify() == TypeClassifier.POINTER)
                    {
                        int pointerCount = ((PointerType)rootType).Pointers;
                        if (op.Length > pointerCount)
                            throw new SemanticException("Unable to dereference a non-pointer", node.Content[op.Length - pointerCount - 1].Position);

                        treeName = "Dereference";
                        dt = op.Length == pointerCount ? ((PointerType)rootType).Type : new PointerType(((PointerType)rootType).Type, pointerCount - op.Length);

                        if (dt.Classify() == TypeClassifier.POINTER && ((SimpleType)dt).Type == SimpleType.DataType.VOID)
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
