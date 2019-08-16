using Whirlwind.Syntax;
using Whirlwind.Types;

using System.Collections.Generic;
using System.Linq;

namespace Whirlwind.Semantic.Visitor
{
    partial class Visitor
    {
        private void _visitInclude(ASTNode node, bool exported)
        {
            var includeSet = new List<string>();
            bool includeAll = false;
            string path = "", name = "";
            TextPosition namePos = new TextPosition();

            foreach (var item in node.Content)
            {
                switch (item.Name)
                {
                    case "TOKEN":
                        {
                            var tk = (TokenNode)item;

                            // only time identifier occurs in
                            // main node is rename
                            if (tk.Tok.Type == "IDENTIFIER")
                            {
                                name = tk.Tok.Value;
                                namePos = item.Position;
                            }                               
                        }
                        break;
                    case "pkg_name":
                        foreach (var elem in ((ASTNode)item).Content)
                        {
                            if (elem is TokenNode tk)
                            {
                                if (tk.Tok.Type == "IDENTIFIER")
                                    path += tk.Tok.Value;
                                else if (tk.Tok.Type == ":")
                                {
                                    path += "/";
                                    continue;
                                }
                            }
                            else if (elem.Name == "pkg_back_move")
                                path += "../";

                            namePos = item.Position;
                        }
                        break;
                    case "include_set":
                        foreach (var elem in ((ASTNode)item).Content.Select(x => ((TokenNode)x).Tok))
                        {
                            if (elem.Type == "IDENTIFIER")
                                includeSet.Add(elem.Value);
                            else if (elem.Type == "...")
                                includeAll = true;
                        }
                        break;
                }
            }

            if (Program.PkgManager.Import(path, out Package pkg))
            {
                var symbolList = new List<Symbol>();

                if (includeAll)
                    symbolList = pkg.ExternalTable.Values.ToList();
                else if (includeSet.Count > 0)
                    symbolList = pkg.ExternalTable.Where(x => includeSet.Contains(x.Key))
                        .Select(x => x.Value)
                        .ToList();
                else
                {
                    string finalName = name;

                    if (finalName == "")
                    {
                        if (path.Contains("/"))
                            finalName = path.Split('/').Last();
                        else
                            finalName = path;
                    }

                    if (_table.AddSymbol(new Symbol(finalName, pkg)))
                    {
                        _nodes.Add(new StatementNode(exported ? "ExportInclude" : "Include"));
                        _nodes.Add(new IdentifierNode(finalName, pkg));
                        MergeBack();

                        if (name != "")
                        {
                            _nodes.Add(new ValueNode("Rename", new VoidType(), name));
                            MergeBack();
                        }   
                    }
                    else
                        throw new SemanticException($"Unable to redeclare symbol by name `{finalName}`", namePos);

                    return;
                }

                for (int i = 0; i < symbolList.Count; i++)
                {
                    var symbol = symbolList[i].Copy();

                    if (!exported)
                        symbol.Modifiers.Remove(Modifier.EXPORTED);

                    // already has exported modifier in it if needed
                    if (!_table.AddSymbol(symbol))
                        throw new SemanticException("Unable to include symbol with same name as existing symbol",
                            ((ASTNode)node.Content[1]).Content[i * 2 + 1].Position);
                }

                if (includeAll)
                {
                    _nodes.Add(new StatementNode(exported ? "ExportIncludeAll" : "IncludeAll"));
                    _nodes.Add(new ValueNode("Package", pkg));
                    MergeBack();
                }
                else
                {
                    _nodes.Add(new StatementNode(exported ? "ExportIncludeSet" : "IncludeSet"));
                    _nodes.Add(new ValueNode("Package", pkg));
                    MergeBack();

                    _nodes.Add(new ExprNode("IncludeSet", new VoidType()));

                    foreach (var symbol in symbolList)
                    {
                        // never modified so ok to not copy
                        _nodes.Add(new IdentifierNode(symbol.Name, symbol.DataType));
                    }

                    MergeBack(symbolList.Count);
                    MergeBack();
                }
            }
            else
                throw new SemanticException("Unable to import the given package", 
                    node.Content.Where(x => x.Name == "pkg_name").First().Position);
        }
    }
}
