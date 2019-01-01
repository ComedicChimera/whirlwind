using System.Collections.Generic;
using System.Linq;
using System;

using Whirlwind.Parser;
using Whirlwind.Types;

namespace Whirlwind.Semantic.Visitor
{
    partial class Visitor
    {
        private void _visitTemplate(ASTNode node, List<Modifier> modifiers)
        {
            _nodes.Add(new BlockNode("Template"));

            _table.AddScope();
            _table.DescendScope();

            var templateVars = new Dictionary<string, List<IDataType>>();

            foreach (var item in node.Content)
            {
                if (item.Name == "template")
                {
                    ASTNode templateVar = (ASTNode)item;

                    string varName = ((TokenNode)templateVar.Content[0]).Tok.Value;

                    if (templateVars.ContainsKey(varName))
                        throw new SemanticException("Template type aliases must have different names", templateVar.Content[0].Position);

                    List<IDataType> restrictors = new List<IDataType>();

                    if (templateVar.Content.Count == 3)
                        restrictors.AddRange(_generateTypeList((ASTNode)templateVar.Content[2]));

                    templateVars.Add(varName, restrictors);
                }
                else if (item.Name == "template_block_decl" || item.Name == "func_decl")
                {
                    foreach (var templateVar in templateVars)
                        _table.AddSymbol(new Symbol(templateVar.Key, new TemplatePlaceholder(templateVar.Key),
                            new List<Modifier>() { Modifier.CONSTANT }));

                    // for method templates
                    if (item.Name == "func_decl")
                        _visitFunction((ASTNode)item, modifiers);
                    else
                        _visitBlockDecl((ASTNode)item, modifiers);

                    MergeToBlock();
                }
            }

            Symbol sym = _table.GetScope().Last();

            _table.AscendScope();

            Action<ASTNode, List<Modifier>> vfn;

            switch (sym.DataType.Classify())
            {
                case TypeClassifier.FUNCTION:
                    vfn = _visitFunction;
                    break;
                case TypeClassifier.OBJECT:
                    vfn = _visitTypeClass;
                    break;
                case TypeClassifier.INTERFACE:
                    vfn = _visitInterface;
                    break;
                default:
                    vfn = _visitStruct;
                    break;
            }

            var tt = new TemplateType(templateVars.Select(x => new TemplateVariable(x.Key, x.Value)).ToList(), sym.DataType, 
                _decorateEval((ASTNode)((ASTNode)node.Content.Last()).Content[0], vfn));

            _nodes.Add(new IdentifierNode(sym.Name, tt, true));
            MergeBack();

            if (!_table.AddSymbol(new Symbol(sym.Name, tt, modifiers)))
                // pretty much all sub symbols have the same name position
                throw new SemanticException($"Unable to redeclare symbol by name `{sym.Name}`", ((ASTNode)node.Content.Last()).Content[1].Position);
        }

        private TemplateEvaluator _decorateEval(ASTNode node, Action<ASTNode, List<Modifier>> vfn)
        {
            return delegate (Dictionary<string, IDataType> aliases)
            {
                _table.AddScope();
                _table.DescendScope();

                foreach (var alias in aliases)
                    _table.AddSymbol(new Symbol(alias.Key, new TemplateAlias(alias.Value)));

                vfn(node, new List<Modifier>() { Modifier.CONSTANT });
                _nodes.RemoveAt(_nodes.Count - 1);

                IDataType dt = _table.GetScope().Last().DataType;

                _table.AscendScope();
                _table.RemoveScope();

                return dt;
            };
        }
    }
}
