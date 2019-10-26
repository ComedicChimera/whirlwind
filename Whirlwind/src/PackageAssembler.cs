using System;
using System.Collections.Generic;
using System.Linq;

using Whirlwind.Syntax;

namespace Whirlwind
{
    class PackageAssemblyException : Exception
    {
        public string SymbolA, SymbolB;

        public PackageAssemblyException(string dup)
        {
            SymbolA = dup;
            SymbolB = "";
        }

        public PackageAssemblyException(string symA, string symB)
        {
            SymbolA = symA;
            SymbolB = symB;
        }
    }

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

            public string FileName;
            public int ASTLocation;

            public SymbolInfo(string fileName, int astLoc)
            {
                Dependecies = new List<string>();
                Status = ResolutionStatus.UNRESOLVED;

                FileName = fileName;
                ASTLocation = astLoc;
            }

            public SymbolInfo(List<string> deps, string fileName, int astLoc)
            {
                Dependecies = deps;
                Status = ResolutionStatus.UNRESOLVED;

                FileName = fileName;
                ASTLocation = astLoc;
            }
        }

        private Package _package;
        private Dictionary<string, SymbolInfo> _resolvingSymbols;
        private Dictionary<string, List<int>> _annotations;

        private string _currentFileName = "";

        private int _interfBindId = 0;
        private int _variantId = 0;
        private int _overloadId = 0;

        public PackageAssembler(Package pkg)
        {
            _package = pkg;

            _resolvingSymbols = new Dictionary<string, SymbolInfo>();

            _annotations = new Dictionary<string, List<int>>();
        }

        public ASTNode Assemble()
        {
            foreach (var item in _package.Files)
                _processPackage(item.Value, item.Key);

            var result = new ASTNode("whirlwind");

            // add non-coupling annotations
            foreach (var fileName in _package.Files.Keys)
            {
                if (_annotations.ContainsKey(fileName))
                {
                    for (int i = _annotations[fileName].Count - 1; i >= 0; i--)
                    {
                        var ndx = _annotations[fileName][i];
                        var annot = _package.Files[fileName].Content[ndx];

                        if (new[] { "platform", "static_link", "res_name" }.Contains(
                            ((TokenNode)((ASTNode)annot).Content[1]).Tok.Value))
                        {
                            result.Content.Add(_package.Files[fileName].Content[ndx]);
                            _annotations[fileName].RemoveAt(ndx);
                        }
                    }
                }
            }

            foreach (var item in _resolvingSymbols)
            {
                if (item.Value.Status == ResolutionStatus.UNRESOLVED)
                    _resolveSymbol(item.Key, item.Value, result);
            }

            return result;
        }

        private void _resolveSymbol(string name, SymbolInfo info, ASTNode result)
        {
            var fileAST = _package.Files[info.FileName];
            var symbolAST = (ASTNode)fileAST.Content[info.ASTLocation];

            var noSelf = symbolAST.Name == "block_decl" && !new[] { "interface_decl", "struct_decl" }.Contains(symbolAST.Content[0].Name);

            info.Status = ResolutionStatus.RESOLVING;

            foreach (var item in info.Dependecies)
            {
                if (_resolvingSymbols.ContainsKey(item))
                {
                    var dep = _resolvingSymbols[item];

                    if (dep.Status == ResolutionStatus.UNRESOLVED)
                        _resolveSymbol(item, dep, result);
                    // symbol is in resolution as this lookup is occuring => recursive definition (ERROR)
                    else if ((noSelf || name != item) && dep.Status == ResolutionStatus.RESOLVING)
                        throw new PackageAssemblyException(name, item);
                }
            }

            if (info.FileName != _currentFileName)
            {
                result.Content.Add(new ASTNode("$FILE_NAME$" + info.FileName));
                _currentFileName = info.FileName;
            }

            // append annotation before the block it wraps if necessary
            int prevNdx = info.ASTLocation - 1;
            if (_annotations.ContainsKey(info.FileName) && _annotations[info.FileName].Contains(prevNdx))
            {
                result.Content.Add(fileAST.Content[prevNdx]);
                _annotations[info.FileName].Remove(prevNdx);
            }

            result.Content.Add(symbolAST);

            info.Status = ResolutionStatus.RESOLVED;
        }

        private void _addSymbol(string name, SymbolInfo info, bool allowDuplicates = false)
        {
            if (_resolvingSymbols.ContainsKey(name))
            {
                if (allowDuplicates)
                    _resolvingSymbols.Add(name + "$" + _overloadId++, info);
                else
                    throw new PackageAssemblyException(name);
            }
            else
                _resolvingSymbols.Add(name, info);
        }

        private void _processPackage(ASTNode node, string fileName)
        {
            for (int i = 0; i < node.Content.Count; i++)
            {
                if (node.Content[i] is ASTNode anode)
                {
                    if (anode.Name == "annotation")
                    {
                        if (_annotations.ContainsKey(fileName))
                            _annotations[fileName].Add(i);
                        else
                            _annotations[fileName] = new List<int> { i };
                    }                       
                    else
                        _processDecl(anode, fileName, i);
                }
            }
        }

        private void _processDecl(ASTNode node, string fileName, int astLoc)
        {
            switch (node.Name)
            {
                case "variable_decl":
                    _processVarDecl(node, fileName, astLoc);
                    break;
                case "block_decl":
                    {
                        var blockDecl = (ASTNode)node.Content[0];

                        switch (blockDecl.Name)
                        {
                            case "func_decl":
                                _processFuncDecl(blockDecl, fileName, astLoc);
                                break;
                            case "interface_decl":
                                _processInterfDecl(blockDecl, fileName, astLoc);
                                break;
                            case "type_class_decl":
                                _processTypeClassDecl(blockDecl, fileName, astLoc);
                                break;
                            case "struct_decl":
                                _processStructDecl(blockDecl, fileName, astLoc);
                                break;
                            case "variant_decl":
                                _processVariantDecl(blockDecl, fileName, astLoc);
                                break;
                            case "decor_decl":
                                _processDecorDecl(blockDecl, fileName, astLoc);
                                break;
                            case "interface_bind":
                                _processInterfBind(blockDecl, fileName, astLoc);
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

                        _addSymbol(name, new SymbolInfo(fileName, astLoc));
                    }
                    break;
                case "export_decl":
                    _processDecl((ASTNode)node.Content[1], fileName, astLoc);
                    break;
            }
        }

        private void _processVarDecl(ASTNode node, string fileName, int astLoc)
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

            _addSymbol(name, new SymbolInfo(foundDeps, fileName, astLoc));
        }

        private void _processFuncDecl(ASTNode node, string fileName, int astLoc)
        {
            string name = ((TokenNode)node.Content[1]).Tok.Value;
            var info = new SymbolInfo(fileName, astLoc);

            _extractPrototype(node, info.Dependecies);

            _addSymbol(name, info, true);
        }

        private void _processInterfDecl(ASTNode node, string fileName, int astLoc)
        {
            var name = "";
            var info = new SymbolInfo(fileName, astLoc);

            foreach (var item in node.Content)
            {
                if (item is TokenNode tkNode && tkNode.Tok.Type == "IDENTIFIER")
                    name = tkNode.Tok.Value;
                else if (item.Name == "generic_tag")
                    _extractGenericTag((ASTNode)item, info.Dependecies);
                else if (item.Name == "interface_main")
                {
                    foreach (var elem in ((ASTNode)item).Content)
                        _extractPrototype((ASTNode)elem, info.Dependecies);
                }
            }

            _addSymbol(name, info);
        }

        private void _processTypeClassDecl(ASTNode node, string fileName, int astLoc)
        {
            string name = "";
            var info = new SymbolInfo(fileName, astLoc);

            foreach (var item in node.Content)
            {
                if (item is TokenNode tkNode && tkNode.Tok.Type == "IDENTIFIER")
                    name = tkNode.Tok.Value;
                else if (item.Name == "generic_tag")
                    _extractGenericTag((ASTNode)item, info.Dependecies);
                else if (item.Name == "type_class_main")
                {
                    foreach (var elem in ((ASTNode)item).Content)
                    {
                        if (elem is TokenNode tkElem && tkElem.Tok.Type == "IDENTIFIER")
                            info.Dependecies.Add(tkElem.Tok.Value);
                        else if (elem is ASTNode anode)
                        {
                            switch (elem.Name)
                            {
                                case "value_constructor":
                                    _extractAll(anode, info.Dependecies);
                                    break;
                                case "type_id":
                                    _extractAll((ASTNode)anode.Content.Last(), info.Dependecies);
                                    break;
                                case "type_constructor":
                                    foreach (var typeId in anode.Content)
                                    {
                                        if (typeId.Name == "type_id")
                                            _extractAll((ASTNode)((ASTNode)typeId).Content.Last(), info.Dependecies);
                                    }
                                    break;
                            }
                        }                            
                    }
                }
            }

            _addSymbol(name, info);
        }

        private void _processInterfBind(ASTNode node, string fileName, int astLoc)
        {
            var info = new SymbolInfo(fileName, astLoc);

            foreach (var item in node.Content)
            {
                switch (item.Name)
                {
                    case "types":
                    case "implements":
                        _extractAll((ASTNode)item, info.Dependecies);
                        break;
                    case "generic_tag":
                        _extractGenericTag((ASTNode)item, info.Dependecies);
                        break;
                    case "interface_main":
                        foreach (var elem in ((ASTNode)item).Content)
                            _extractPrototype((ASTNode)elem, info.Dependecies);
                        break;
                }
            }

            _addSymbol("$INTERF_BIND$" + _interfBindId++, info);
        }

        private void _processStructDecl(ASTNode node, string fileName, int astLoc)
        {
            string name = ((TokenNode)node.Content[1]).Tok.Value;
            var info = new SymbolInfo(fileName, astLoc);

            foreach (var item in node.Content)
            {
                if (item.Name == "generic_tag")
                    _extractGenericTag((ASTNode)item, info.Dependecies);
                else if (item.Name == "struct_main")
                {
                    foreach (var elem in ((ASTNode)item).Content)
                    {
                        if (elem.Name == "struct_var")
                            _extractAll((ASTNode)((ASTNode)elem).Content.Where(x => x.Name == "types").First(), info.Dependecies);
                        else if (elem.Name == "constructor_decl")
                            _extractPrototype((ASTNode)elem, info.Dependecies);
                    }
                }
            }

            _addSymbol(name, info);
        }

        private void _processVariantDecl(ASTNode node, string fileName, int astLoc)
        {
            string name = "$VARIANT$" + _variantId++;
            var info = new SymbolInfo(fileName, astLoc);

            info.Dependecies.Add(((TokenNode)node.Content[2]).Tok.Value);

            if (node.Content[1].Name == "variant")
                _extractAll((ASTNode)node.Content[1], info.Dependecies);
            else if (node.Content[1].Name == "variant_list")
            {
                foreach (var item in ((ASTNode)node.Content[1]).Content)
                {
                    if (item.Name == "variant")
                        _extractAll((ASTNode)item, info.Dependecies);
                }
            }

            _addSymbol(name, info);
        }

        private void _processDecorDecl(ASTNode node, string fileName, int astLoc)
        {
            string name = ((TokenNode)((ASTNode)node.Content[1]).Content[1]).Tok.Value;
            var info = new SymbolInfo(fileName, astLoc);

            _extractAll((ASTNode)node.Content[0], info.Dependecies);
            _extractPrototype((ASTNode)node.Content[1], info.Dependecies);

            _addSymbol(name, info);
        }

        private void _extractPrototype(ASTNode node, List<string> foundDeps)
        {
            foreach (var item in node.Content)
            {
                if (item.Name == "generic_tag")
                    _extractGenericTag((ASTNode)item, foundDeps);
                else if (item.Name == "args_decl_list")
                {
                    foreach (var elem in ((ASTNode)item).Content)
                    {
                        if (elem.Name == "decl_arg" || elem.Name == "ending_arg")
                        {
                            foreach (var argElem in ((ASTNode)elem).Content)
                            {
                                if (argElem.Name == "extension")
                                    _extractAll((ASTNode)argElem, foundDeps);
                            }
                        }
                    }
                }
                else if (item.Name == "types")
                    _extractAll((ASTNode)item, foundDeps);
            }
        }

        private void _extractGenericTag(ASTNode genDecl, List<string> foundDeps)
        {
            foreach (var item in genDecl.Content)
            {
                if (item.Name == "generic")
                {
                    var anode = (ASTNode)item;

                    if (anode.Content.Count > 1)
                        _extractAll((ASTNode)anode.Content[2], foundDeps);
                }
            }
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
