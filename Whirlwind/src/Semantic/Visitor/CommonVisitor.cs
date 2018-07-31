using Whirlwind.Parser;
using Whirlwind.Types;
using static Whirlwind.Semantic.Checker.Checker;

using System.Collections.Generic;
using System.Linq;

namespace Whirlwind.Semantic.Visitor
{
    partial class Visitor
    {
        public IDataType _generateType(ASTNode node)
        {
            IDataType dt = new SimpleType(SimpleType.DataType.NULL);
            int pointers = 0;

            foreach (var subNode in node.Content)
            {
                if (subNode.Name == "TOKEN")
                {
                    var tokenNode = ((TokenNode)subNode);

                    switch (tokenNode.Tok.Type)
                    {
                        case "*":
                            pointers++;
                            break;
                        case "IDENTIFIER":
                            if (_table.Lookup(tokenNode.Tok.Value, out Symbol symbol))
                            {
                                if (symbol.DataType.Classify() == "TEMPLATE_ALIAS")
                                {
                                    dt = ((TemplateAlias)symbol.DataType).ReplacementType;
                                }
                                if (!new[] { "MODULE", "INTERFACE", "STRUCT"}.Contains(symbol.DataType.Classify()))
                                    throw new SemanticException("Identifier data type must be a module or an interface", tokenNode.Position);
                                dt = symbol.DataType.Classify() == "MODULE" ? ((ModuleType)symbol.DataType).GetInstance() : symbol.DataType;
                            }
                            else
                            {
                                throw new SemanticException($"Undefined Identifier: '{tokenNode.Tok.Value}'", tokenNode.Position);
                            }
                            break;
                    }
                }
                else if (subNode.Name == "template_spec")
                {
                    if (dt.Classify() != "TEMPLATE")
                        throw new SemanticException("Unable to apply template specifier to non-template type", subNode.Position);
                    dt = _generateTemplate((TemplateType)dt, (ASTNode)subNode);
                }
                else
                {
                    dt = _generateBaseType((ASTNode)subNode);
                }
            }

            if (pointers != 0)
            {
                dt = new PointerType(dt, pointers);
            }

            return dt;
        }

        public List<IDataType> _generateTypeList(ASTNode node)
        {
            var dataTypes = new List<IDataType>();
            foreach (var subNode in node.Content)
            {
                if (subNode.Name == "types")
                    dataTypes.Add(_generateType((ASTNode)subNode));
            }
            return dataTypes;
        }

        public void _visitIterator(ASTNode node)
        {
            // expects previous node to be the iterable value
            var iterable = _nodes.Last().Type;
            var iteratorTypes = new List<IDataType>();

            if (Iterable(iterable))
            {
                if (iterable is IIterable)
                    iteratorTypes.AddRange((iterable as IIterable).GetIterator());
                // should never fail
                else if (((ModuleInstance)iterable).GetProperty("__next__", out Symbol method))
                {
                    iteratorTypes.AddRange(((FunctionType)method.DataType).ReturnTypes);
                }
            }
            else
                throw new SemanticException("Unable to create iterator over non-iterable value", node.Position);

            // all are tokens
            string[] identifiers = node.Content.Select(x => ((TokenNode)x).Tok)
                .Where(x => x.Type == "IDENTIFIER")
                .Select(x => x.Value)
                .ToArray();

            if (identifiers.Length != iteratorTypes.Count)
                throw new SemanticException("Base iterator and it's alias's don't match", node.Position);

            for (int i = 0; i < identifiers.Length; i++)
            {
                _table.AddSymbol(new Symbol(identifiers[i], iteratorTypes[i]));
            }

            _nodes.Add(new TreeNode(
                "Iterator",
                new SimpleType(SimpleType.DataType.NULL),
                Enumerable.Range(0, identifiers.Length)
                    .Select(i => new ValueNode("Identifier", iteratorTypes[i], identifiers[i]))
                    .Select(x => x as ITypeNode)
                    .ToList()
            ));
        }

        private IDataType _generateTemplate(TemplateType baseType, ASTNode templateSpecifier)
        {
            // template_spec -> type_list
            var typeList = _generateTypeList((ASTNode)templateSpecifier.Content[1]);
            
            if (baseType.CreateTemplate(typeList, out IDataType dt))
                return dt;
            else
                throw new SemanticException("Invalid type specifier for the given template", templateSpecifier.Position);

        }
    }
}
