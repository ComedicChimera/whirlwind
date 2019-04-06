﻿using System.Collections.Generic;

using Whirlwind.Parser;
using Whirlwind.Types;

namespace Whirlwind.Semantic.Visitor
{
    partial class Visitor
    {
        private void _visitStruct(ASTNode node, List<Modifier> modifiers)
        {
            _nodes.Add(new BlockNode("Struct"));
            TokenNode name = (TokenNode)node.Content[1];

            var structType = new StructType(name.Tok.Value, false);

            // descent for self referential >:(
            _table.AddScope();
            _table.DescendScope();

            // declare self referential type (ok early, b/c reference)
            _table.AddSymbol(new Symbol(name.Tok.Value, structType, new List<Modifier> { Modifier.CONSTANT }));
            // since struct members are all variables
            _selfNeedsPointer = true;

            // needs a default constructor
            bool needsDefaultConstr = true;

            foreach (var subNode in ((ASTNode)node.Content[3]).Content)
            {
                if (subNode.Name == "struct_var")
                {
                    var processingStack = new List<TokenNode>();

                    foreach (var item in ((ASTNode)subNode).Content)
                    {
                        if (item.Name == "TOKEN" && ((TokenNode)item).Tok.Type == "IDENTIFIER")
                            processingStack.Add((TokenNode)item);
                        else if (item.Name == "types")
                        {
                            DataType type = _generateType((ASTNode)item);

                            foreach (var member in processingStack)
                            {
                                if (!structType.AddMember(member.Tok.Value, type))
                                    throw new SemanticException("Structs cannot contain duplicate members", member.Position);
                            }
                        }
                    }
                }
                else if (subNode.Name == "constructor_decl")
                {
                    ASTNode decl = (ASTNode)subNode;

                    var fnType = _visitConstructor(decl, new List<Modifier> { Modifier.CONSTANT });

                    if (!structType.AddConstructor(fnType))
                        throw new SemanticException("Unable to declare duplicate constructors", decl.Content[2].Position);

                    needsDefaultConstr = false;
                }
            }

            if (needsDefaultConstr)
                structType.AddConstructor(new FunctionType(new List<Parameter>(), new SimpleType(), false));

            _nodes.Add(new IdentifierNode(name.Tok.Value, structType, true));
            MergeBack();

            _table.AscendScope();

            if (!_table.AddSymbol(new Symbol(name.Tok.Value, structType, modifiers)))
                throw new SemanticException($"Unable to redeclare symbol by name `{name.Tok.Value}`", name.Position);

            // undo self needs pointer
            _selfNeedsPointer = false;
        }

        private FunctionType _visitConstructor(ASTNode decl, List<Modifier> modifiers)
        {
            List<Parameter> args = new List<Parameter>();

            _nodes.Add(new BlockNode("Constructor"));

            foreach (var item in decl.Content)
            {
                if (item.Name == "args_decl_list")
                    args = _generateArgsDecl((ASTNode)item);
                else if (item.Name == "func_body")
                {


                    _nodes.Add(new IncompleteNode((ASTNode)item));
                    MergeToBlock();
                }
            }

            FunctionType ft = new FunctionType(args, new SimpleType(), false);

            _nodes.Add(new ValueNode("ConstructorSignature", ft));
            MergeBack();

            return ft;
        }
    }
}