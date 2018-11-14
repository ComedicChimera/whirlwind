using Whirlwind.Parser;

namespace Whirlwind.Semantic.Visitor
{
    partial class Visitor
    {
        private void _visitBlockDecl(ASTNode node)
        {
            ASTNode root = (ASTNode)node.Content[0];

            switch (root.Name)
            {
                case "func_decl":
                    _visitFunction(root);
                    break;
                case "interface_decl":
                    _visitInterface(root);
                    break;
                case "struct_decl":
                    _visitStruct(root);
                    break;
            }
        }

        private void _visitInterface(ASTNode node)
        {

        }

        private void _visitStruct(ASTNode node)
        {

        }



    }
}
