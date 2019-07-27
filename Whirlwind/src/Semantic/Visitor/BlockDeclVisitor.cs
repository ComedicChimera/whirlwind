using Whirlwind.Parser;
using Whirlwind.Types;

using System.Collections.Generic;
using System.Linq;

namespace Whirlwind.Semantic.Visitor
{
    partial class Visitor
    {
        private void _visitBlockDecl(ASTNode node, List<Modifier> modifiers)
        {
            ASTNode root = (ASTNode)node.Content[0];

            var genericVars = new List<GenericVariable>();
            var namePosition = new TextPosition();
            if (root.Content.Count > 2 && root.Content[2].Name == "generic_tag")
            {
                genericVars = _primeGeneric((ASTNode)root.Content[2]);
                namePosition = root.Content[1].Position;
            }

            switch (root.Name)
            {
                case "type_class_decl":
                    _visitTypeClass(root, modifiers, genericVars);
                    return;
                case "func_decl":
                    _visitFunction(root, modifiers);
                    break;
                case "interface_decl":
                    _visitInterface(root, modifiers);
                    break;
                case "interface_bind":
                    _visitInterfaceBind(root);
                    break;
                case "struct_decl":
                    _visitStruct(root, modifiers);
                    break;
                case "decor_decl":
                    _visitDecorator(root, modifiers);
                    break;
                case "variant_decl":
                    _visitVariant(root);
                    break;
            }

            if (genericVars.Count > 0)
                _makeGeneric(root, genericVars, modifiers, _table.GetScope().Last(), namePosition);
        }

        private void _visitDecorator(ASTNode node, List<Modifier> modifiers)
        {
            var fnNode = (ASTNode)node.Content[1];

            if (fnNode.Content[2].Name == "generic_tag")
                throw new SemanticException("Unable to apply decorator to a generic function", fnNode.Content[2].Position);

            _visitFunction(fnNode, modifiers);

            FunctionType fnType = (FunctionType)((TreeNode)_nodes.Last()).Nodes[0].Type;

            _nodes.Add(new BlockNode("Decorator"));
         
            foreach (var item in ((ASTNode)node.Content[0]).Content)
            {
                if (item.Name == "expr")
                {
                    _visitExpr((ASTNode)item);

                    if (_nodes.Last().Type.Classify() == TypeClassifier.FUNCTION)
                    {
                        FunctionType decorType = (FunctionType)_nodes.Last().Type;

                        if (decorType.MatchArguments(new ArgumentList(new List<DataType>() { fnType.MutableCopy() })))
                        {
                            // check for void decorators
                            if (!(decorType.ReturnType is FunctionType))
                                throw new SemanticException("A decorator must return a function", item.Position);

                            // allows decorator to override function return type ;)
                            if (!fnType.Coerce(decorType.ReturnType))
                            {
                                _table.Lookup(((TokenNode)((ASTNode)node.Content[1]).Content[1]).Tok.Value, out Symbol sym);

                                if (sym.DataType is FunctionGroup fg)
                                    fg.Functions = fg.Functions.Select(x => x.Equals(fnType) ? decorType.ReturnType : x)
                                        .Select(x => (FunctionType)x).ToList();
                                else
                                    sym.DataType = decorType.ReturnType;
                            }

                            MergeBack();
                        }
                        else
                            throw new SemanticException("This decorator is not valid for the given function", item.Position);
                    }
                    else
                        throw new SemanticException("Unable to use non-function as a decorator", item.Position);
                }
            }

            PushToBlock();
        }
    }
}
