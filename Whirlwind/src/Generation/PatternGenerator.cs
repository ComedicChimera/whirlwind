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

            public PMatrixElement GetElement(int i, int j)
                => matrix[i][j];

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
            => dt is TupleType || (dt is CustomInstance ci && ci.Parent.IsReferenceType());

        private PatternMatrix _constructTuplePatternMatrix(List<DataType> columns, List<BlockNode> caseNodes)
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
                    _makePMatrixRow(pMat, i, columns, caseExpr.Nodes);
                // assume is tuple expression
                else if (caseExpr.Type is TupleType ctt)
                {
                    var tuple = _generateExpr(caseExpr);

                    for (int j = 0; j < ctt.Types.Count; i++)
                    {
                        var tupleElem = _getLLVMStructMember(tuple, j, ctt.Types[j], $"tuple_elem.{i}");

                        if (!columns[j].Equals(ctt.Types[j]))
                            tupleElem = _cast(tupleElem, ctt.Types[j], columns[j]);

                        pMat.SetElement(i, j, tupleElem);
                    }
                }
            }

            return pMat;
        }

        private void _makePMatrixRow(PatternMatrix pMat, int row, List<DataType> columnTypes, List<ITypeNode> nodes)
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
                        pMat.SetElement(row, j, _getSwitchableValue(node, columnTypes[j]));
                        break;
                }
            }
        }

        private bool _generatePatternBranch(PatternMatrix p, List<DataType> types, List<LLVMValueRef> selectItems, 
            int column, IEnumerable<int> validRows, List<LLVMBasicBlockRef> caseBlocks, LLVMBasicBlockRef defaultBlock)
        {
            var selectItem = selectItems[column];

            if (_needsHash(types[column]))
                selectItem = _getHash(selectItem, types[column]);

            var values = new List<Tuple<int, LLVMValueRef>>();
            var neutrals = new List<int>();

            for (int r = 0; r < p.rows; r++)
            {
                if (validRows.Contains(r))
                {
                    var matElem = p.GetElement(r, column);

                    if (matElem.eType == PMatElementType.VALUE)
                        values.Add(new Tuple<int, LLVMValueRef>(r, matElem.eValue));
                    else
                        neutrals.Add(r);
                }
            }

            bool fullDefault = neutrals.Count == 0;
            bool noDefault = true;

            if (column == p.columns - 1)
            {
                var validCaseNumbers = values.Select(x => x.Item1);

                _switchBetween(selectItem, values.Select(x => x.Item2).ToList(),
                    caseBlocks.Where((x, i) => validCaseNumbers.Contains(i)).ToList(),
                    fullDefault ? defaultBlock : caseBlocks[neutrals.First()]);

                noDefault = !fullDefault;
            }
            else
            {
                LLVMBasicBlockRef patternDefault;
                if (fullDefault)
                    patternDefault = defaultBlock;
                else
                    patternDefault = LLVM.AppendBasicBlock(_currFunctionRef, "patternDefault");

                var switchStmt = LLVM.BuildSwitch(_builder, selectItem, patternDefault, (uint)values.Count);

                foreach (var item in values)
                {
                    var patternCase = LLVM.AppendBasicBlock(_currFunctionRef, "pattern_case");
                    LLVM.PositionBuilderAtEnd(_builder, patternCase);

                    noDefault &= _generatePatternBranch(p, types, selectItems, column + 1, neutrals.Append(item.Item1),
                        caseBlocks, defaultBlock);

                    switchStmt.AddCase(item.Item2, patternCase);
                }

                if (!fullDefault)
                {
                    LLVM.PositionBuilderAtEnd(_builder, patternDefault);

                    noDefault &= _generatePatternBranch(p, types, selectItems, column + 1, neutrals,
                        caseBlocks, defaultBlock);
                }
                else
                    noDefault = false;
            }

            return noDefault;
        }

        private void _switchBetween(LLVMValueRef switchExpr, List<LLVMValueRef> onValNodes, 
            List<LLVMBasicBlockRef> caseBlocks, LLVMBasicBlockRef defaultBlock)
        {
            var switchStmt = LLVM.BuildSwitch(_builder, switchExpr, defaultBlock, (uint)caseBlocks.Count);

            for (int i = 0; i < onValNodes.Count; i++)
            {
                LLVM.AddCase(switchStmt, onValNodes[i], caseBlocks[i]);
            }
        }

        // TODO: test non-pattern type classes (for behavior)
        private LLVMValueRef _getSwitchableValue(ITypeNode node, DataType switchType)
        {
            var vref = _generateExpr(node);

            if (!switchType.Equals(node))
                vref = _cast(vref, node.Type, switchType);

            if (_needsHash(node.Type))
                vref = _getHash(vref, switchType);

            return vref;
        }

        private void _moveBlocksToEnd(List<LLVMBasicBlockRef> caseBlocks, LLVMBasicBlockRef defaultBlock)
        {
            foreach (var caseBlock in caseBlocks)
                LLVM.MoveBasicBlockAfter(caseBlock, LLVM.GetLastBasicBlock(_currFunctionRef));

            LLVM.MoveBasicBlockAfter(defaultBlock, LLVM.GetLastBasicBlock(_currFunctionRef));
        }

        private List<Dictionary<string, GeneratorSymbol>> _getCaseVariables(PatternMatrix p, List<DataType> types, 
            List<LLVMValueRef> selectItems, List<LLVMBasicBlockRef> caseBlocks)
        {
            var scopes = new List<Dictionary<string, GeneratorSymbol>>();

            for (int r = 0; r < p.rows; r++)
            {
                scopes.Add(new Dictionary<string, GeneratorSymbol>());

                for (int c = 0; c < p.columns; c++)
                {
                    var pVar = p.GetElement(r, c);
                    LLVM.PositionBuilderAtEnd(_builder, caseBlocks[r]);

                    if (pVar.eType == PMatElementType.NAME)
                    {
                        if (_isReferenceType(types[c]))
                            scopes[r].Add(pVar.eName, new GeneratorSymbol(selectItems[c]));
                        else
                        {
                            var varRef = LLVM.BuildAlloca(_builder, selectItems[c].TypeOf(), pVar.eName);
                            LLVM.BuildStore(_builder, selectItems[c], varRef);

                            scopes[r].Add(pVar.eName, new GeneratorSymbol(varRef, true));
                        }
                    }
                }
            }

            return scopes;
        }

        private bool _generatePatternMatch(ITypeNode selectExprNode, List<BlockNode> caseNodes,
            List<LLVMBasicBlockRef> caseBlocks, LLVMBasicBlockRef defaultBlock, out List<Dictionary<string, GeneratorSymbol>> caseScopes)
        {
            var selectExpr = _generateExpr(selectExprNode);
            bool noDefault = false;

            if (selectExprNode.Type is TupleType tt)
            {
                var patternMatrix = _constructTuplePatternMatrix(tt.Types, caseNodes);

                var selectItems = new List<LLVMValueRef>();
                for (int i = 0; i < tt.Types.Count; i++)
                    selectItems.Add(_getLLVMStructMember(selectExpr, i, tt.Types[i], $"root_pattern_elem{i}"));

                noDefault = _generatePatternBranch(patternMatrix, tt.Types, selectItems, 0,
                    Enumerable.Range(0, caseBlocks.Count), caseBlocks, defaultBlock);

                caseScopes = _getCaseVariables(patternMatrix, tt.Types, selectItems, caseBlocks);
            }
            else if (selectExprNode.Type is CustomInstance ci)
            {
                // TODO: the type class situation
                var cVal = LLVM.BuildStructGEP(_builder, selectExpr, 1, "root_cval_elem_ptr_tmp");
                cVal = LLVM.BuildLoad(_builder, cVal, "root_cval_tmp");

                var caseExprs = caseNodes
                    .SelectMany(x => x.Nodes)
                    .Select(x => (ExprNode)x)
                    .ToList();

                var switchStmt = LLVM.BuildSwitch(_builder, cVal, defaultBlock, (uint)ci.Parent.Instances.Count);
                for (int i = 0; i < ci.Parent.Instances.Count; i++)
                {
                    var switchOnVal = LLVM.ConstInt(LLVM.Int16Type(), (ulong)i, new LLVMBool(0));
                    var instance = ci.Parent.Instances[i];

                    var validCaseNodes = caseExprs
                        .Where(x => x.Type.Equals(instance))
                        .ToList();

                    var selectItems = new List<LLVMValueRef>();

                    if (instance is CustomAlias ca)
                    {
                        var selectItem = LLVM.BuildStructGEP(_builder, selectExpr, 0, "root_alias_elem_ptr_tmp");
                        selectItem = LLVM.BuildLoad(_builder, selectItem, "root_alias_i8ptr_tmp");

                        selectItem = LLVM.BuildBitCast(_builder, selectItem, LLVM.PointerType(_convertType(ca.Type), 0), "root_alias_ptr_tmp");

                        if (!_isReferenceType(ca.Type))
                            selectItem = LLVM.BuildLoad(_builder, selectItem, "root_alias_tmp");

                        if (_needsHash(ca.Type))
                            selectItem = _getHash(selectItem, ca.Type);

                        selectItems.Add(selectItem);
                    }
                    else if (instance is CustomNewType cnt)
                    {
                        if (cnt.Values.Count == 1)
                        {
                            var selectItem = LLVM.BuildStructGEP(_builder, selectExpr, 0, "root_algval_elem_ptr_tmp");
                            selectItem = LLVM.BuildLoad(_builder, selectItem, "root_algval_i8ptr_tmp");

                            var algType = cnt.Values[0];

                            selectItem = LLVM.BuildBitCast(_builder, selectItem, 
                                LLVM.PointerType(_convertType(algType), 0), "root_algval_ptr_tmp");

                            if (!_isReferenceType(algType))
                                selectItem = LLVM.BuildLoad(_builder, selectItem, "root_algval_tmp");

                            if (_needsHash(algType))
                                selectItem = _getHash(selectItem, algType);

                            selectItems.Add(selectItem);
                        }
                        else if (cnt.Values.Count > 1)
                        {

                        }
                        // extract conditionally and store
                    }

                    // get type class pattern matrix (for instance)
                    // generate branch
                    // get case scopes & combine with overall list
                }

                caseScopes = null; // TEMPORARY
            }
            else
                caseScopes = null; // NEVER REACHED

            _moveBlocksToEnd(caseBlocks, defaultBlock);

            return noDefault;
        }
    }
}
