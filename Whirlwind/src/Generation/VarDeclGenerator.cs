using System;
using System.Collections.Generic;
using System.Linq;

using Whirlwind.Types;
using Whirlwind.Semantic;

using LLVMSharp;

namespace Whirlwind.Generation
{
    partial class Generator
    {
        private void _generateVarDecl(StatementNode stNode)
        {
            var uninitializedVars = new List<IdentifierNode>();

            foreach (var item in ((ExprNode)stNode.Nodes[0]).Nodes)
            {
                var varNode = (ExprNode)item;
                var idNode = (IdentifierNode)varNode.Nodes[0];

                if (varNode.Nodes.Count == 1)
                    uninitializedVars.Add(idNode);
                else
                {
                    var initExpr = _generateExpr(varNode.Nodes[1]);

                    if (!idNode.Type.Equals(varNode.Nodes[1].Type))
                        initExpr = _cast(initExpr, idNode.Type, varNode.Nodes[1].Type);

                    if (_isPointerType(idNode.Type))
                        _scopes.Last()[idNode.IdName] = new GeneratorSymbol(initExpr);
                    else
                    {
                        var varAlloc = LLVM.BuildAlloca(_builder, _convertType(idNode.Type), idNode.IdName);
                        LLVM.BuildStore(_builder, initExpr, varAlloc);

                        _scopes.Last()[idNode.IdName] = new GeneratorSymbol(varAlloc, true);
                    }
                }
            }

            var totalInitExpr = _generateExpr(stNode.Nodes[1]);
            var tieType = stNode.Nodes[1].Type;

            if (_isPointerType(tieType))
            {
                for (int i = 0; i < uninitializedVars.Count; i++)
                {
                    var item = uninitializedVars[i];
                    
                    if (item.Type.Equals(tieType))
                    {
                        if (i > 0)
                            _scopes.Last()[item.IdName] = new GeneratorSymbol(_copy(totalInitExpr));
                        else
                            _scopes.Last()[item.IdName] = new GeneratorSymbol(totalInitExpr);
                    }
                    else
                        _scopes.Last()[item.IdName] = new GeneratorSymbol(_cast(totalInitExpr, item.Type, tieType));
                }
            }
            else
            {
                foreach (var item in uninitializedVars)
                {
                    var varAlloc = LLVM.BuildAlloca(_builder, _convertType(item.Type), item.IdName);

                    if (item.Type.Equals(tieType))
                        LLVM.BuildStore(_builder, totalInitExpr, varAlloc);
                    else
                        LLVM.BuildStore(_builder, _cast(totalInitExpr, item.Type, tieType), varAlloc);

                    _scopes.Last()[item.IdName] = new GeneratorSymbol(varAlloc, true);
                }
            }           
        }

        private void _generateConstDecl(StatementNode stNode)
        {
            var uninitializedVars = new List<IdentifierNode>();

            foreach (var item in ((ExprNode)stNode.Nodes[0]).Nodes)
            {
                var varNode = (ExprNode)item;
                var idNode = (IdentifierNode)varNode.Nodes[0];

                if (varNode.Nodes.Count == 1)
                    uninitializedVars.Add(idNode);
                else
                {
                    var initExpr = _generateExpr(varNode.Nodes[1]);

                    if (!idNode.Type.Equals(varNode.Nodes[1].Type))
                        initExpr = _cast(initExpr, idNode.Type, varNode.Nodes[1].Type);

                    _scopes.Last()[idNode.IdName] = new GeneratorSymbol(initExpr);
                }
            }

            var totalInitExpr = _generateExpr(stNode.Nodes[1]);
            var tieType = stNode.Nodes[1].Type;

            for (int i = 0; i < uninitializedVars.Count; i++)
            {
                var item = uninitializedVars[i];

                if (item.Type.Equals(tieType))
                {
                    if (i > 0)
                        _scopes.Last()[item.IdName] = new GeneratorSymbol(_copy(totalInitExpr));
                    else
                        _scopes.Last()[item.IdName] = new GeneratorSymbol(totalInitExpr);
                }
                else
                    _scopes.Last()[item.IdName] = new GeneratorSymbol(_cast(totalInitExpr, item.Type, tieType));
            }
        }
    }
}
