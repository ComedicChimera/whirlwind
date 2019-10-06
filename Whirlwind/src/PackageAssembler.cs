using System;
using System.Collections.Generic;
using System.Linq;

using Whirlwind.Syntax;

namespace Whirlwind
{
    class PackageAssembler
    {
        enum ResolutionStatus
        {
            UNRESOLVED,
            RESOLVING,
            RESOLVED
        }

        class SymbolInfo
        {
            public List<string> Dependecies;
            public ResolutionStatus Status;

            public int ASTNumber;
            public int ASTLocation;

            public SymbolInfo(int astNum, int astLoc)
            {
                Dependecies = new List<string>();
                Status = ResolutionStatus.UNRESOLVED;

                ASTNumber = astNum;
                ASTLocation = astLoc;
            }

            public SymbolInfo(List<string> deps, int astNum, int astLoc)
            {
                Dependecies = deps;
                Status = ResolutionStatus.UNRESOLVED;

                ASTNumber = astNum;
                ASTLocation = astLoc;
            }
        }

        private Package _package;
        private Dictionary<string, SymbolInfo> _resolvingSymbols;

        private int _currentFileFlag = -1;

        public PackageAssembler(Package pkg)
        {
            _package = pkg;

            _resolvingSymbols = new Dictionary<string, SymbolInfo>();
        }

        public ASTNode Assemble()
        {
            for (int i = 0; i < _package.Files.Count; i++)
                _processPackage(_package.Files.Values.ElementAt(i), i);

            var result = new ASTNode("whirlwind");

            foreach (var item in _resolvingSymbols)
            {
                if (item.Value.Status != ResolutionStatus.RESOLVED)
                    _resolveSymbol(item.Key, item.Value, result);
            }

            for (int astNum = 0; astNum < _package.Files.Count; astNum++)
            {
                var ast = _package.Files.Values.ElementAt(astNum);

                // no need to update file flag b/c we already know
                // the next ast won't match up
                if (astNum != _currentFileFlag)
                    result.Content.Add(new ASTNode("$FILE_FLAG$" + astNum));

                for (int i = 0; i < ast.Content.Count; i++)
                {
                    if (_resolvingSymbols.All(x => x.Value.ASTNumber != astNum && x.Value.ASTLocation != i))
                        result.Content.Add(ast.Content[i]);
                }
            }

            return result;
        }

        private void _resolveSymbol(string name, SymbolInfo info, ASTNode result)
        {
            info.Status = ResolutionStatus.RESOLVING;

            foreach (var item in info.Dependecies)
            {
                if (_resolvingSymbols.ContainsKey(item))
                {
                    var dep = _resolvingSymbols[item];

                    if (dep.Status == ResolutionStatus.UNRESOLVED)
                        _resolveSymbol(item, dep, result);
                }
            }

            if (info.ASTNumber != _currentFileFlag)
            {
                result.Content.Add(new ASTNode("$FILE_FLAG$" + info.ASTNumber));
                _currentFileFlag = info.ASTNumber;
            }

            result.Content.Add(_package.Files.Values.ElementAt(info.ASTNumber).Content[info.ASTLocation]);
        }

        private void _processPackage(ASTNode node, int astNum)
        {
            for (int i = 0; i < node.Content.Count; i++)
            {
                if (node.Content[i] is ASTNode anode && anode.Name != "annotation")
                    _processDecl(anode, astNum, i);
            }
        }

        private void _processDecl(ASTNode node, int astNum, int astLoc)
        {
            switch (node.Name)
            {
                case "variable_decl":
                    _processVarDecl(node, astNum, astLoc);
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

                        _resolvingSymbols.Add(name, new SymbolInfo(astNum, astLoc));
                    }
                    break;
                case "export_decl":
                    _processDecl((ASTNode)node.Content[1], astNum, astLoc);
                    break;
            }
        }

        private void _processVarDecl(ASTNode node, int astNum, int astLoc)
        {
            string name = "";
            var foundDeps = new List<string>();

            foreach (var item in node.Content)
            {
                if (item is ASTNode anode)
                {
                    if (anode.Name == "var")
                    {
                        foreach (var elem in anode.Content)
                        {
                            if (elem is TokenNode tk)
                                name = tk.Tok.Value;
                            else if (elem.Name == "var_id")
                                name = ((TokenNode)((ASTNode)elem).Content[0]).Tok.Value;
                            else if (elem.Name == "extension")
                                _extractAll((ASTNode)elem, foundDeps);
                            else if (elem.Name == "variable_initializer")
                                _extractAll((ASTNode)((ASTNode)elem).Content[1], foundDeps);
                        }
                    }
                    else if (anode.Name == "extension")
                        _extractAll(anode, foundDeps);
                    else if (anode.Name == "variable_initializer")
                        _extractAll((ASTNode)anode.Content[1], foundDeps);
                }
            }

            _resolvingSymbols[name] = new SymbolInfo(foundDeps, astNum, astLoc);
        }

        private void _extractAll(ASTNode node, List<string> foundDeps)
        {
            foreach (var item in node.Content)
            {
                if (item is ASTNode anode)
                {
                    switch (anode.Name)
                    {
                        // add any special cases for expressions where lookup isn't necessary
                        case "expr_var":
                            _extractAll((ASTNode)anode.Content[2], foundDeps);
                            break;
                        case "lambda":
                            _extractAll((ASTNode)anode.Content.Last(), foundDeps);
                            break;
                        default:
                            _extractAll(anode, foundDeps);
                            break;
                    }
                }
                else if (item is TokenNode tkNode && tkNode.Tok.Type == "IDENTIFIER")
                    foundDeps.Add(tkNode.Tok.Value);
            }
        }
    }
}
