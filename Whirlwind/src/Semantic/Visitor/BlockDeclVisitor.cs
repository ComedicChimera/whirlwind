using System.Linq;
using System.Collections.Generic;

using Whirlwind.Syntax;
using Whirlwind.Types;

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

                if (root.Name == "interface_decl" || root.Name == "struct_decl")
                {
                    _isGenericSelfContext = true;

                    // should always work
                    _table.AddSymbol(new Symbol("$GENERIC_SELF", new GenericSelfType(_namePrefix +((TokenNode)root.Content[1]).Tok.Value, 
                        genericVars)));
                }                   
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

            if (_wrapsNextAnnotBlock && (root.Name == "struct_decl" || root.Name == "func_decl"))
            {
                if (_implName != "")
                {
                    _typeImpls[_implName] = ((BlockNode)_nodes.Last()).Nodes[0].Type;
                    _implName = "";
                }

                var lastNodes = new List<ITypeNode>();

                // remove last two nodes
                for (int i = 0; i < 2; i++)
                {
                    lastNodes.Add(_nodes.Last());
                    _nodes.RemoveLast();
                }

                // put nodes in correct order
                lastNodes.Reverse();

                // add to annotated block
                _nodes.Add(new BlockNode("AnnotatedBlock"));
                ((BlockNode)_nodes.Last()).Block.AddRange(lastNodes);

                // clear annotated block state
                _wrapsNextAnnotBlock = false;
            }
        }
    }
}
