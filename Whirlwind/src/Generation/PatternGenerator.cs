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
        enum PMatElementType
        {
            VALUE,
            NAME,
            IGNORE
        }

        class PMatrixElement
        {
            public readonly PMatElementType eType;
            public readonly LLVMValueRef eValue;
            public readonly string eName;

            public PMatrixElement()
            {
                eType = PMatElementType.IGNORE;
            }

            public PMatrixElement(string name)
            {
                eType = PMatElementType.NAME;
                eName = name;
            }

            public PMatrixElement(LLVMValueRef val)
            {
                eType = PMatElementType.VALUE;
                eValue = val;
            }
        }

        class PatternMatrix
        {
            public readonly int rows, columns;
            public PMatrixElement[][] matrix;

            public PatternMatrix(int rows_, int columns_)
            {
                rows = rows_;
                columns = columns_;

                matrix = new PMatrixElement[rows][];

                for (int i = 0; i < rows; i++)
                {
                    matrix[i] = new PMatrixElement[columns];

                    for (int j = 0; j < columns; j++)
                    {
                        matrix[i][j] = new PMatrixElement();
                    }
                }
            }

            public void SetElement(int i, int j, LLVMValueRef vref)
            {
                matrix[i][j] = new PMatrixElement(vref); 
            }

            public void SetElement(int i, int j, string name)
            {
                matrix[i][j] = new PMatrixElement(name);
            }
        }

        private bool _isPatternType(DataType dt)
            => dt is CustomInstance || dt is TupleType;

        // arity checks handled externally
        private PatternMatrix _constructPatternMatrix(List<DataType> columns, List<BlockNode> caseNodes)
        {
            var caseExprs = caseNodes
                .SelectMany(x => x.Nodes)
                .Select(x => (ExprNode)x)
                .ToList();

            var pMat = new PatternMatrix(caseExprs.Count, columns.Count);

            for (int i = 0; i < caseExprs.Count; i++)
            {
                var caseExpr = caseExprs[i];

                if (caseExpr.Name == "TuplePattern")
                    _makePMatrixRow(pMat, i, columns[i], caseExpr.Nodes);
                else if (caseExpr.Name == "TypeClassPattern")
                    _makePMatrixRow(pMat, i, columns[i], caseExpr.Nodes.Skip(1).ToList());
                // assume is expression and populate accordingly
                else
                {

                }
            }

            return pMat;
        }

        private void _makePMatrixRow(PatternMatrix pMat, int row, DataType columnType, List<ITypeNode> nodes)
        {
            for (int j = 0; j < nodes.Count; j++)
            {
                var node = nodes[j];

                switch (node.Name)
                {
                    case "PatternSymbol":
                        pMat.SetElement(row, j, ((ValueNode)node).Value);
                        break;
                    // ignore this value (no need to set anything)
                    case "_":
                        break;
                    default:
                        pMat.SetElement(row, j, _cast(_generateExpr(node), node.Type, columnType));
                        break;
                }
            }
        }

        private void _generatePatternMatch(LLVMValueRef selectExpr, DataType selectType, List<BlockNode> caseNodes,
            LLVMBasicBlockRef[] caseBlocks, LLVMBasicBlockRef defaultBlock)
        {

        }
    }
}
