using Whirlwind.Parser;
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
                if (subNode.Name == "unary_atom")
                    _visitUnaryAtom((ASTNode)subNode);
                else if (subNode.Name != "TOKEN")
                    _visitExpr((ASTNode)subNode);
            }
        }

        private void _visitArithmetic(ASTNode node)
        {

        }

        private void _visitExponent(ASTNode node)
        {
            if (node.Content.Count == 1)
            {
                _visitUnaryAtom((ASTNode)node.Content[0]);
                return;
            }

            int count = 0;
            IDataType rootType = new SimpleType();

            foreach (var item in node.Content)
            {
                if (item.Name == "unary_atom")
                {
                    count++;
                    _visitUnaryAtom((ASTNode)item);
                    if (count == 1)
                        rootType = _nodes.Last().Type;
                    else
                        CheckOperand(ref rootType, _nodes.Last().Type, "^", item.Position);
                }
            }

            _nodes.Add(new TreeNode("^", rootType));
            PushForward(count);
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
                    else if (HasOverload(rootType, "__invert__", out IDataType newDt))
                    {
                        _nodes.Add(new TreeNode("OverloadCall", newDt));
                        // push root type
                        PushForward();
                        // push identifier
                        _nodes.Add(new IdentifierNode("__invert__", new SimpleType(), false));
                        MergeBack();
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
