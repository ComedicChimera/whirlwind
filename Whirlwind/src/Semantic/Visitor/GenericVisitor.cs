using System.Collections.Generic;
using System.Linq;
using System;

using Whirlwind.Parser;
using Whirlwind.Types;

namespace Whirlwind.Semantic.Visitor
{
    partial class Visitor
    {
        private List<GenericVariable> _primeGeneric(ASTNode tag)
        {
            var genericVars = new Dictionary<string, List<DataType>>();

            _table.AddScope();
            _table.DescendScope();

            foreach (var item in tag.Content)
            {
                if (item.Name == "generic")
                {
                    ASTNode genericVar = (ASTNode)item;

                    string varName = ((TokenNode)genericVar.Content[0]).Tok.Value;

                    if (genericVars.ContainsKey(varName))
                        throw new SemanticException("Generic type aliases must have different names", genericVar.Content[0].Position);

                    List<DataType> restrictors = new List<DataType>();

                    if (genericVar.Content.Count == 3)
                        restrictors.AddRange(_generateTypeList((ASTNode)genericVar.Content[2]));

                    genericVars.Add(varName, restrictors);

                    _table.AddSymbol(new Symbol(varName, new GenericPlaceholder(varName)));
                }
            }

            // exit generic scope later

            return genericVars.Select(x => new GenericVariable(x.Key, x.Value)).ToList();   
        }

        private void _makeGeneric(ASTNode root, List<GenericVariable> genericVars, List<Modifier> modifiers, TextPosition position)
        {
            _nodes.Add(new BlockNode("Generic"));

            Symbol sym = _table.GetScope().Last();

            // exit generic scope
            _table.AscendScope();

            Action<ASTNode, List<Modifier>> vfn;

            switch (sym.DataType.Classify())
            {
                case TypeClassifier.FUNCTION:
                    vfn = _visitFunction;
                    break;
                case TypeClassifier.INTERFACE:
                    vfn = _visitInterface;
                    break;
                case TypeClassifier.TYPE_CLASS:
                    vfn = _visitGenerateTypeClass;
                    break;
                default:
                    vfn = _visitStruct;
                    break;
            }

            var gt = new GenericType(genericVars, sym.DataType, 
                _decorateEval(root, vfn));

            _nodes.Add(new IdentifierNode(sym.Name, gt));
            MergeBack();

            // push in block declaration
            PushToBlock();

            if (!_table.AddSymbol(new Symbol(sym.Name, gt, modifiers)))
                // pretty much all sub symbols have the same name position
                throw new SemanticException($"Unable to redeclare symbol by name `{sym.Name}`", position);
        }

        private void _visitGenerateTypeClass(ASTNode node, List<Modifier> modifiers)
        {
            _table.AddScope();
            _table.DescendScope();

            _visitTypeClass(node, modifiers, new List<GenericVariable>());

            Symbol sym = _table.GetScope().First();

            _table.AscendScope();

            _table.AddSymbol(sym);
        }

        private GenericEvaluator _decorateEval(ASTNode node, Action<ASTNode, List<Modifier>> vfn)
        {
            return delegate (Dictionary<string, DataType> aliases, GenericType parent)
            {
                if (parent.DataType is CustomType ct)
                {
                    _table.AddScope(new Capture(ct.Instances
                        .Where(x => x is CustomNewType)
                        .Select(x => ((CustomNewType)x).Name)
                        .ToList()));
                }
                else
                    _table.AddScope();

                _table.DescendScope();

                foreach (var alias in aliases)
                    _table.AddSymbol(new Symbol(alias.Key, new GenericAlias(alias.Value)));

                var newContent = node.Content.Where(x => x.Name != "generic_tag").ToList();
                node = new ASTNode(node.Name);
                node.Content = newContent;

                vfn(node, new List<Modifier>());
                BlockNode generateNode = (BlockNode)_nodes.Last();

                _nodes.RemoveAt(_nodes.Count - 1);

                DataType dt = _table.GetScope().Last().DataType;

                if (parent.GenericInterface.Body != null)
                {
                    var interf = dt.GetInterface();

                    // create block node to hold generate interface
                    _nodes.Add(new BlockNode("GenerateInterfHolder"));
                    _collectInterfaceMethods(interf, parent.GenericInterface.Body, true);
                    // remove holder
                    _nodes.RemoveAt(_nodes.Count - 1);

                    if (parent.GenericInterface.StandardImplements != null)
                    {
                        foreach (var item in parent.GenericInterface.StandardImplements)
                        {
                            if (!item.Derive(dt))
                                throw new SemanticException("This generic generate is not valid for the given interface", node.Content[1].Position);
                        }
                    }

                    if (parent.GenericInterface.GenericImplements != null)
                    {
                        foreach (var item in parent.GenericInterface.GenericImplements)
                        {
                            if (item.CreateGeneric(aliases.Values.ToList(), out DataType impl))
                            {
                                if (((InterfaceType)impl).Derive(dt))
                                    continue;
                            }

                            throw new SemanticException("This generic generate is not valid for the given interface", node.Content[1].Position);
                        }
                    }
                    
                }

                _table.AscendScope();
                _table.RemoveScope();

                return new GenericGenerate(dt, generateNode);
            };
        }

        private void _visitVariant(ASTNode node)
        {
            TokenNode id = (TokenNode)node.Content[2];

            if (!_table.Lookup(id.Tok.Value, out Symbol symbol))
                throw new SemanticException($"Undefined symbol `{id.Tok.Value}`", id.Position);

            if (symbol.DataType.Classify() == TypeClassifier.GENERIC)
            {
                if (node.Content[1].Name == "variant")
                {
                    var types = _generateTypeList((ASTNode)((ASTNode)node.Content[1]).Content[1]);
                    var gt = (GenericType)symbol.DataType;

                    if (!gt.AddVariant(types))
                        throw new SemanticException("The variant type list is not valid for the base generic", node.Content[1].Position);

                    if (gt.DataType.Classify() == TypeClassifier.INTERFACE)
                    {
                        if (!((InterfaceType)gt.DataType).GetFunction(id.Tok.Value, out Symbol _))
                            throw new SemanticException("Unable to create sub variant of non-existent method", id.Position);
                    }

                    _nodes.Add(new BlockNode("Variant"));
                    // won't fail because add variant succeeded
                    gt.CreateGeneric(types, out DataType dt);
                    // remove the last generate added because its body is not accurate to the variant
                    gt.Generates.RemoveAt(gt.Generates.Count - 1);

                    _nodes.Add(new ValueNode("VariantGenerate", dt));
                    _nodes.Add(new IdentifierNode(id.Tok.Value, gt)); // identifier after so visit function works ;)
                    MergeBack(2);

                    _nodes.Add(new IncompleteNode((ASTNode)node.Content.Last()));
                    MergeToBlock();

                }
                else
                    throw new SemanticException("Unable to implement multilevel variance with a variant depth of one", node.Content[1].Position);
            }
            else
                throw new SemanticException("Unable to add variant to non-generic", id.Position);
        }
    }
}
