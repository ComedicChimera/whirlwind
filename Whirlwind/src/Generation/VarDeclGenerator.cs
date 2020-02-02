using System.Collections.Generic;

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
                        initExpr = _cast(initExpr, varNode.Nodes[1].Type, idNode.Type);

                    if (_isReferenceType(idNode.Type))
                        _setVar(idNode.IdName, initExpr);
                    else
                    {
                        var varAlloc = LLVM.BuildAlloca(_builder, _convertType(idNode.Type), idNode.IdName);
                        LLVM.BuildStore(_builder, initExpr, varAlloc);

                        _setVar(idNode.IdName, varAlloc, true);
                    }
                }
            }

            var totalInitExpr = _generateExpr(stNode.Nodes[1]);
            var tieType = stNode.Nodes[1].Type;

            if (_isReferenceType(tieType))
            {
                for (int i = 0; i < uninitializedVars.Count; i++)
                {
                    var item = uninitializedVars[i];
                    
                    if (item.Type.Equals(tieType))
                    {
                        if (i > 0)
                            _setVar(item.IdName, _copyRefType(totalInitExpr));
                        else
                            _setVar(item.IdName, totalInitExpr);
                    }
                    else
                        _setVar(item.IdName, _cast(totalInitExpr, tieType, item.Type));
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
                        LLVM.BuildStore(_builder, _cast(totalInitExpr, tieType, item.Type), varAlloc);

                    _setVar(item.Name, varAlloc, true);
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
                        initExpr = _cast(initExpr, varNode.Nodes[1].Type, idNode.Type);

                    _setVar(idNode.IdName, initExpr);
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
                        _setVar(item.IdName, _copyRefType(totalInitExpr));
                    else
                        _setVar(item.IdName, totalInitExpr);
                }
                else
                    _setVar(item.IdName, _cast(totalInitExpr, tieType, item.Type));
            }
        }
    }
}
