using System.Linq;

using Whirlwind.Syntax;
using Whirlwind.Types;

namespace Whirlwind.Semantic.Visitor
{
    partial class Visitor
    {
        private void _visitAnnotation(ASTNode node)
        {
            string annotation = ((TokenNode)node.Content[1]).Tok.Value, 
                value = "";

            if (node.Content.Count == 3)
                value = ((TokenNode)node.Content[2]).Tok.Value.Trim('\"');

            if (!_isValidAnnotation(annotation, value != ""))
                throw new SemanticException("Invalid annotation", node.Position);

            _processAnnotation(annotation, value, node.Content.Last().Position);

            if (!_wrapsNextAnnotBlock)
                _flags[annotation] = value;

            _nodes.Add(new StatementNode("Annotation"));
            _nodes.Add(new ValueNode("AnnotationName", new NoneType(), annotation));
            MergeBack();

            if (value != "")
            {
                _nodes.Add(new ValueNode("AnnotationValue", new SimpleType(SimpleType.SimpleClassifier.STRING), value));
                MergeBack();
            }
        }

        private bool _isValidAnnotation(string annot, bool hasValue)
        {
            if (hasValue && new[] { "impl", "platform", "static_link", "res_name" }.Contains(annot))
                return true;
            else if (new[] { "extern", "intrinsic", "packed" }.Contains(annot))
                return true;

            return false;
        }

        private void _processAnnotation(string annot, string value, TextPosition valuePosition)
        {
            switch (annot)
            {
                case "intrinsic":
                case "extern":
                    _wrapsNextAnnotBlock = true;
                    _functionCanHaveNoBody = true;
                    break;
                case "packed":
                    _wrapsNextAnnotBlock = true;
                    break;
                case "impl":
                    _wrapsNextAnnotBlock = true;
                    _implName = value;
                    break;
                case "res_name":
                    if (!value.EndsWith(".llvm") || value.Contains("/"))
                        throw new SemanticException("Invalid value for output name", valuePosition);
                    break;
            }
        }
    }
}
