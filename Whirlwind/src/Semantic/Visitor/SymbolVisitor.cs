using System.Collections.Generic;

using Whirlwind.Parser;
using Whirlwind.Types;

namespace Whirlwind.Semantic.Visitor
{
    partial class Visitor
    {
        private List<Symbol> _doSymbolPass(ASTNode node)
        {
            var symbols = new List<Symbol>();

            foreach (var item in node.Content)
            {
                if (item.Name == "block_decl")
                {
                    ASTNode decl = (ASTNode)((ASTNode)item).Content[0];

                    switch (decl.Name)
                    {
                        case "enum_decl":
                            _visitEnum(decl, new List<Modifier>());
                            break;
                        case "struct_decl":
                            _visitStruct(decl, new List<Modifier>());
                            break;
                        case "func_decl":
                            symbols.Add(getFunctionSignature(decl, new List<Modifier>()));
                            break;
                        case "type_class_decl":
                            break;
                        case "decor_decl":
                            break;
                        case "interface_decl":
                            break;
                    }
                }
                else if (item.Name == "variable_decl")
                    _visitVarDecl((ASTNode)item, new List<Modifier>());
            }

            return symbols;
        }

        private Symbol getFunctionSignature(ASTNode function, List<Modifier> modifiers)
        {
            bool isAsync = false;
            string name = "";
            var arguments = new List<Parameter>();
            IDataType rtType = new SimpleType();

            TextPosition namePosition = new TextPosition();

            foreach (var item in function.Content)
            {
                switch (item.Name)
                {
                    case "TOKEN":
                        {
                            Token tok = ((TokenNode)item).Tok;

                            switch (tok.Type)
                            {
                                case "ASYNC":
                                    isAsync = true;
                                    break;
                                case "IDENTIFIER":
                                    name = tok.Value;
                                    namePosition = item.Position;
                                    break;
                            }
                        }
                        break;
                    case "args_decl_list":
                        arguments = _generateArgsDecl((ASTNode)item);
                        break;
                    case "types":
                        rtType = _generateType((ASTNode)item);
                        break;
                }
            }

            return new Symbol(name, new FunctionType(arguments, rtType, isAsync)); ;
        }

        private void _getDecoratorSignature(ASTNode decor, List<Modifier> modifiers)
        {
            Symbol baseSymbol = getFunctionSignature(decor, modifiers);


        }
    }
}
