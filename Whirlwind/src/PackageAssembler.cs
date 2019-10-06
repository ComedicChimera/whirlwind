using System;
using System.Collections.Generic;
using System.Linq;

using Whirlwind.Syntax;

namespace Whirlwind
{
    class PackageAssembler
    {
        private Package _package;
        private List<string> _idOrder;
        private Dictionary<string, int> _idPositions;

        public PackageAssembler(Package pkg)
        {
            _package = pkg;

            _idOrder = new List<string>();
            _idPositions = new Dictionary<string, int>();
        }

        public ASTNode Assemble()
        {
            foreach (var item in _package.Files)
                _processPackage(item.Value);

            return new ASTNode("");
        }

        private void _processPackage(ASTNode node)
        {
            for (int i = 0; i < node.Content.Count; i++)
            {
                if (node.Content[i] is ASTNode anode && anode.Name != "annotation")
                    _processDecl(anode, i);
            }
        }

        private void _processDecl(ASTNode node, int pos)
        {
            switch (node.Name)
            {
                case "variable_decl":
                    _processVarDecl(node, pos);
                    break;
                case "block_decl":
                    {
                        var blockDecl = (ASTNode)node.Content[0];

                        switch (blockDecl.Name)
                        {
                            case "func_decl":
                                break;
                            case "interface_decl":
                                break;
                            case "type_class_decl":
                                break;
                            case "struct_decl":
                                break;
                            case "variant_decl":
                                break;
                            case "decor_decl":
                                break;
                            case "interface_bind":
                                break;
                        }
                    }
                    break;
                case "include_stmt":
                    {
                        string name = "";

                        foreach (var item in node.Content)
                        {
                            if (item.Name == "pkg_name")
                                name = ((TokenNode)((ASTNode)item).Content.Last()).Tok.Value;
                            else if (item is TokenNode tkNode && tkNode.Tok.Type == "IDENTIFIER")
                                name = tkNode.Tok.Value;
                        }

                        _idPositions[name] = pos;
                    }
                    break;
                case "export_decl":
                    _processDecl((ASTNode)node.Content[1], pos);
                    break;
            }
        }

        private void _processVarDecl(ASTNode node, int pos)
        {
            foreach (var item in node.Content)
            {
                if (item is ASTNode anode)
                {
                    if (anode.Name == "var")
                    {
                        foreach (var elem in anode.Content)
                        {
                            if (elem is TokenNode tk)
                                _idPositions[tk.Tok.Value] = pos;
                            else if (elem.Name == "var_id")
                                _idPositions[((TokenNode)((ASTNode)elem).Content[0]).Tok.Value] = pos;
                            else if (elem.Name == "extension")
                                _extractAll((ASTNode)elem);
                        }
                    }
                    else if (anode.Name == "extension")
                    {
                        _extractAll(anode);
                        break;
                    }                        
                }
            }
        }

        

        private void _extractAll(ASTNode node)
        {
            foreach (var item in node.Content)
            {
                if (item is ASTNode anode)
                {
                    switch (anode.Name)
                    {
                        // add any special cases for expressions where lookup isn't necessary
                        case "expr_var":
                            _extractAll((ASTNode)anode.Content[2]);
                            break;
                        case "lambda":
                            _extractAll((ASTNode)anode.Content.Last());
                            break;
                        default:
                            _extractAll(anode);
                            break;
                    }
                }
                else if (item is TokenNode tkNode && tkNode.Tok.Type == "IDENTIFIER")
                    _idOrder.Add(tkNode.Tok.Value);
            }
        }
    }
}
