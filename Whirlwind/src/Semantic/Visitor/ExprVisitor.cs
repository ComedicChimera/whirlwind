﻿using Whirlwind.Parser;
using Whirlwind.Types;

using static Whirlwind.Semantic.Checker.Checker;

using System.Linq;

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
                        _nodes.Add(new TreeNode("InlineCompare", _nodes.Last().Type));
                        PushForward(3);

                        var content = ((TreeNode)_nodes.Last()).Nodes;

                        if (!new SimpleType(SimpleType.DataType.BOOL).Coerce(content[0].Type))
                            throw new SemanticException("Comparison expression of inline comparison must evaluate to a boolean", node.Content[0].Position);

                        if (content[1].Type != content[2].Type)
                            throw new SemanticException("Possible results of inline comparison must be the same type", ((ASTNode)subNode).Content.Last().Position);
                    }
                    else
                    {
                        _nodes.Add(new TreeNode("NullCoalesce", _nodes.Last().Type));
                        PushForward(2);

                        if (((TreeNode)_nodes.Last()).Nodes[0].Type != ((TreeNode)_nodes.Last()).Nodes[1].Type)
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
                        _nodes.Add(new TreeNode(_getOpTreeName(tokenType), rootType));
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
                    _visitArithmetic((ASTNode)subNode);

                if (hitFirst)
                {
                    CheckOperand(ref rootType, _nodes.Last().Type, op, subNode.Position);
                    MergeBack();
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
                        _nodes.Add(new TreeNode(_getOpTreeName(tokenType), rootType));
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
                            _nodes.Add(new TreeNode("Not", new SimpleType(SimpleType.DataType.BOOL)));
                            PushForward();
                        }
                        else if (_nodes.Last().Type.Classify() == "SIMPLE_TYPE")
                        {
                            _nodes.Add(new TreeNode("Not", _nodes.Last().Type));
                            PushForward();
                        }
                        else if (HasOverload(_nodes.Last().Type, "__not__", out IDataType returnType))
                        {
                            _nodes.Add(new TreeNode("Not", returnType));
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

            using (var enumerator = node.Content.GetEnumerator())
            {
                while (enumerator.MoveNext())
                {
                    var subNode = enumerator.Current;

                    if (subNode.Name == "TOKEN")
                    {
                        string tempOp = "";

                        do
                        {
                            tempOp += ((TokenNode)subNode).Tok.Type;
                            enumerator.MoveNext();
                            subNode = enumerator.Current;
                        } while (subNode.Name == "TOKEN");

                        if (tempOp != op)
                        {
                            _nodes.Add(new TreeNode(_getOpTreeName(tempOp), rootType));
                            PushForward();

                            op = tempOp;

                            if (!hitFirst)
                                hitFirst = true;
                        }
                    }

                    // will always be an ASTNode because of do/while loop
                    _visitArithmetic((ASTNode)subNode);

                    if (hitFirst)
                    {
                        CheckOperand(ref rootType, _nodes.Last().Type, op, subNode.Position);
                        MergeBack();
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
                        _nodes.Add(new TreeNode(_getOpTreeName(tokenType), rootType));
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
                    if (Modifiable() && Numeric(rootType))
                    {
                        treeName = (postfix ? "Postfix" : "Prefix") + "Increment";
                        dt = rootType;
                    }
                    else
                        throw new SemanticException("Increment operator is not valid on non-numeric types", node.Content[postfix ? 2 : 0].Position);
                    break;
                case "--":
                    if (Modifiable() && Numeric(rootType))
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
                        _nodes.Add(new TreeNode("ChangeSign", newDt));
                        // push root type
                        PushForward();
                        return;
                    }
                    else
                        throw new SemanticException("Unable to change sign of non-numeric type", node.Content[0].Position);
                    break;
                case "&":
                    if (new[] { "FUNCTION", "MODULE", "STRUCT", "TEMPLATE", "INTERFACE" }.Contains(rootType.Classify()))
                        throw new SemanticException("The given object is not reference able", node.Content[0].Position);
                    treeName = "Reference";
                    if (rootType.Classify() == "POINTER")
                    {
                        ((PointerType)rootType).Pointers++;
                        dt = rootType;
                    }
                    else
                        dt = new PointerType(rootType, 1);             
                    break;
                // dereference
                default:
                    if (rootType.Classify() == "POINTER")
                    {
                        int pointerCount = ((PointerType)rootType).Pointers;
                        if (op.Length > pointerCount)
                            throw new SemanticException("Unable to dereference a non-pointer", node.Content[op.Length - pointerCount - 1].Position);
                        treeName = "Dereference";
                        dt = op.Length == pointerCount ? ((PointerType)rootType).Type : new PointerType(((PointerType)rootType).Type, pointerCount - op.Length);
                    }
                    else
                        throw new SemanticException("Unable to dereference a non-pointer", node.Content[op.Length - 1].Position);
                    break;
            }

            _nodes.Add(new TreeNode(treeName, dt));
            PushForward();
        }
    }
}