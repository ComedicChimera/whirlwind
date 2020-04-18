using LLVMSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using Whirlwind.Semantic;
using Whirlwind.Types;
using static Whirlwind.Semantic.Checker.Checker;

namespace Whirlwind.Generation
{
    partial class Generator
    {
        // ------------------------------
        // SWITCH CONTROL FLOW GENERATION
        // ------------------------------

        private delegate LLVMValueRef SwitchValueComparer(LLVMValueRef a, LLVMValueRef b);

        class SwitchBuilder
        {
            private readonly DataType _switchType;
            private readonly LLVMValueRef _switchExpr;
            private readonly bool _useSwitch;

            private readonly SwitchValueComparer _switchComparer;

            private List<LLVMValueRef> _onValNodes;
            private List<LLVMBasicBlockRef> _caseBlocks;
            private LLVMBasicBlockRef _defaultBlock;

            public SwitchBuilder(DataType switchType, LLVMValueRef switchExpr, LLVMBasicBlockRef defaultBlock)
            {
                _switchType = switchType;
                _switchExpr = switchExpr;
                _useSwitch = true;

                _onValNodes = new List<LLVMValueRef>();
                _caseBlocks = new List<LLVMBasicBlockRef>();
                _defaultBlock = defaultBlock;
            }

            public SwitchBuilder(DataType switchType, LLVMValueRef switchExpr, LLVMBasicBlockRef defaultBlock, 
                SwitchValueComparer comparer)
            {
                _switchType = switchType;
                _switchExpr = switchExpr;
                _useSwitch = false;

                _switchComparer = comparer;

                _onValNodes = new List<LLVMValueRef>();
                _caseBlocks = new List<LLVMBasicBlockRef>();
                _defaultBlock = defaultBlock;
            }

            public void AddCase(LLVMValueRef onValNode, LLVMBasicBlockRef caseBlock)
            {
                _onValNodes.Add(onValNode);
                _caseBlocks.Add(caseBlock);
            }

            public void Build(LLVMBuilderRef _builder, LLVMValueRef _currFunctionRef)
            {
                if (_caseBlocks.Count == 0)
                {
                    LLVM.BuildBr(_builder, _defaultBlock);
                    return;
                }                   

                if (_useSwitch)
                {
                    if (_caseBlocks.Count == 1)
                    {
                        var cmpResult = LLVM.BuildICmp(_builder, LLVMIntPredicate.LLVMIntEQ, _switchExpr, _onValNodes[0], "switch_cmp");
                        LLVM.BuildCondBr(_builder, cmpResult, _caseBlocks[0], _defaultBlock);
                        return;
                    }

                    var switchStmt = LLVM.BuildSwitch(_builder, _switchExpr, _defaultBlock, (uint)_caseBlocks.Count);

                    for (int i = 0; i < _caseBlocks.Count; i++)
                        switchStmt.AddCase(_onValNodes[i], _caseBlocks[i]);
                }
                else
                {
                    var nextBlock = LLVM.AppendBasicBlock(_currFunctionRef, "switch_case");

                    for (int i = 0; i < _onValNodes.Count; i++)
                    {
                        var cmpResult = _switchComparer(_switchExpr, _onValNodes[i]);

                        LLVM.BuildCondBr(_builder, cmpResult, _caseBlocks[i], nextBlock);

                        if (i < _onValNodes.Count - 1)
                            LLVM.PositionBuilderAtEnd(_builder, nextBlock);

                        if (i == _onValNodes.Count - 2)
                            nextBlock = _defaultBlock;
                        else
                            nextBlock = LLVM.AppendBasicBlock(_currFunctionRef, "switch_case");
                    }
                }
            }
        }

        private Tuple<LLVMValueRef, bool> _getSwitchableValue(ITypeNode node, DataType switchType)
            => _getSwitchableValue(_generateExpr(node), switchType, node.Type);

        // TODO: test non-pattern type classes (for behavior)
        private Tuple<LLVMValueRef, bool> _getSwitchableValue(LLVMValueRef vref, DataType switchType, DataType nodeType)
        {
            if (!switchType.Equals(nodeType))
                vref = _cast(vref, nodeType, switchType);

            if (Hashable(switchType))
            {
                if (_needsHash(switchType))
                {
                    var hashResult = _getHash(vref, switchType);
                    return hashResult;
                }
            }

            return new Tuple<LLVMValueRef, bool>(vref, vref.IsConstant());
        }

        private SwitchValueComparer _getSwitchComparer(DataType dt)
        {
            if (dt is SimpleType st)
            {
                var simpleClass = _getSimpleClass(st);

                switch (simpleClass)
                {
                    case 0:
                        return (a, b) => LLVM.BuildICmp(_builder, LLVMIntPredicate.LLVMIntEQ, a, b, "switch_cmp");
                    case 1:
                        return (a, b) => LLVM.BuildFCmp(_builder, LLVMRealPredicate.LLVMRealOEQ, a, b, "switch_cmp");
                }
            }

            // TODO: get comparison operator in general case

            // temporary
            return (a, b) => _ignoreValueRef();
        }

        private void _switchBetween(LLVMValueRef switchExpr, List<LLVMValueRef> onValNodes,
            List<LLVMBasicBlockRef> caseBlocks, LLVMBasicBlockRef defaultBlock, DataType switchType, bool useSwitch)
        {
            SwitchBuilder swb;

            if (useSwitch)
                swb = new SwitchBuilder(switchType, switchExpr, defaultBlock);
            else
                swb = new SwitchBuilder(switchType, switchExpr, defaultBlock, _getSwitchComparer(switchType));

            for (int i = 0; i < caseBlocks.Count; i++)
                swb.AddCase(onValNodes[i], caseBlocks[i]);

            swb.Build(_builder, _currFunctionRef);
        }

        // ----------------
        // PATTERN MATCHING
        // ----------------

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
            public readonly int Rows, Columns;
            public bool[] SwitchableColumns;

            private readonly PMatrixElement[][] _matrix;

            public PatternMatrix(int rows, int columns)
            {
                Rows = rows;
                Columns = columns;

                _matrix = new PMatrixElement[Rows][];

                for (int i = 0; i < Rows; i++)
                {
                    _matrix[i] = new PMatrixElement[Columns];

                    for (int j = 0; j < Columns; j++)
                    {
                        _matrix[i][j] = new PMatrixElement();
                    }
                }

                SwitchableColumns = Enumerable.Range(0, Columns).Select(x => true).ToArray();
            }

            public PMatrixElement GetElement(int i, int j)
                => _matrix[i][j];

            public void SetElement(int i, int j, LLVMValueRef vref)
            {
                _matrix[i][j] = new PMatrixElement(vref); 
            }

            public void SetElement(int i, int j, string name)
            {
                _matrix[i][j] = new PMatrixElement(name);
            }
        }

        private bool _isPatternType(DataType dt)
            => dt is TupleType || (dt is CustomInstance ci && ci.Parent.IsReferenceType());

        private PatternMatrix _constructTuplePatternMatrix(List<DataType> columns, List<ExprNode> caseExprs)
        {
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

                        var res = _getSwitchableValue(tupleElem, columns[j], ctt.Types[j]);
                        pMat.SwitchableColumns[j] &= res.Item2;

                        pMat.SetElement(i, j, res.Item1);
                    }
                }
            }

            return pMat;
        }

        private PatternMatrix _constructTypeClassPatternMatrix(List<DataType> columns, List<ExprNode> caseExprs)
        {
            var pMat = new PatternMatrix(caseExprs.Count, columns.Count);

            for (int i = 0; i < caseExprs.Count; i++)
            {
                var caseExpr = caseExprs[i];

                if (caseExpr.Name == "TypeClassPattern")
                    _makePMatrixRow(pMat, i, columns, caseExprs.Skip(1));
                else
                {
                    List<DataType> caseExprTypes;
                    if (caseExpr.Type is CustomAlias ca)
                        caseExprTypes = new List<DataType> { ca.Type };
                    // assume is custom new type (only valid case)
                    else
                        caseExprTypes = ((CustomNewType)caseExpr.Type).Values;

                    var caseItems = _getTypeClassValues(_generateExpr(caseExpr), (CustomInstance)caseExpr.Type);
                    
                    for (int j = 0; j < caseExprs.Count; j++)
                    {
                        var res = _getSwitchableValue(caseItems[j], columns[j], caseExprTypes[j]);

                        pMat.SwitchableColumns[j] &= res.Item2;
                        pMat.SetElement(i, j, res.Item1);
                    }
                }
            }

            return pMat;
        }

        private void _makePMatrixRow(PatternMatrix pMat, int row, List<DataType> columnTypes, IEnumerable<ITypeNode> nodes)
        {
            int j = 0;
            foreach (var node in nodes)
            {
                switch (node.Name)
                {
                    case "PatternSymbol":
                        pMat.SetElement(row, j, ((ValueNode)node).Value);
                        break;
                    // ignore this value (no need to set anything)
                    case "_":
                        break;
                    default:
                        {
                            var res = _getSwitchableValue(node, columnTypes[j]);
                            pMat.SetElement(row, j, res.Item1);

                            pMat.SwitchableColumns[j] &= res.Item2;
                        }
                        
                        break;
                }

                j++;
            }
        }

        private bool _generatePatternBranch(PatternMatrix p, List<DataType> types, List<LLVMValueRef> selectItems, 
            int column, IEnumerable<int> validRows, List<LLVMBasicBlockRef> caseBlocks, LLVMBasicBlockRef defaultBlock)
        {
            var selectItem = selectItems[column];

            if (Hashable(types[column]) && _needsHash(types[column]))
                selectItem = _getHash(selectItem, types[column]).Item1;         

            var values = new List<Tuple<int, LLVMValueRef>>();
            var neutrals = new List<int>();

            for (int r = 0; r < p.Rows; r++)
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

            if (column == p.Columns - 1)
            {
                var validCaseNumbers = values.Select(x => x.Item1);

                _switchBetween(selectItem, values.Select(x => x.Item2).ToList(),
                    caseBlocks.Where((x, i) => validCaseNumbers.Contains(i)).ToList(),
                    fullDefault ? defaultBlock : caseBlocks[neutrals.First()],
                    types[column], p.SwitchableColumns[column]);

                noDefault = !fullDefault;
            }
            else
            {

                // if no values, just move to next branch
                if (values.Count == 0)
                {
                    return _generatePatternBranch(p, types, selectItems, column + 1, neutrals,
                            caseBlocks, defaultBlock);
                }

                LLVMBasicBlockRef patternDefault;
                if (fullDefault)
                    patternDefault = defaultBlock;
                else
                    patternDefault = LLVM.AppendBasicBlock(_currFunctionRef, "pattern_default");

                SwitchBuilder swb;
                if (p.SwitchableColumns[column])
                    swb = new SwitchBuilder(types[column], selectItem, patternDefault);
                else
                    swb = new SwitchBuilder(types[column], selectItem, patternDefault, _getSwitchComparer(types[column]));

                var parentBlock = LLVM.GetInsertBlock(_builder);

                foreach (var item in values)
                {
                    var patternCase = LLVM.AppendBasicBlock(_currFunctionRef, "pattern_case");
                    LLVM.PositionBuilderAtEnd(_builder, patternCase);

                    noDefault &= _generatePatternBranch(p, types, selectItems, column + 1, neutrals.Append(item.Item1),
                        caseBlocks, defaultBlock);

                    swb.AddCase(item.Item2, patternCase);
                }

                LLVM.PositionBuilderAtEnd(_builder, parentBlock);
                // will leave us primed in the default block for generation :D
                swb.Build(_builder, _currFunctionRef);

                if (!fullDefault)
                    noDefault &= _generatePatternBranch(p, types, selectItems, column + 1, neutrals,
                            caseBlocks, defaultBlock);
                else
                    noDefault = false;
            }

            return noDefault;
        }

        // --------------------------------
        // PATTERN MATCH GENERATION HELPERS
        // --------------------------------

        private void _moveBlocksToEnd(List<LLVMBasicBlockRef> caseBlocks, LLVMBasicBlockRef defaultBlock)
        {
            foreach (var caseBlock in caseBlocks)
                LLVM.MoveBasicBlockAfter(caseBlock, LLVM.GetLastBasicBlock(_currFunctionRef));

            LLVM.MoveBasicBlockAfter(defaultBlock, LLVM.GetLastBasicBlock(_currFunctionRef));
        }

        private List<Dictionary<string, GeneratorSymbol>> _getCaseVariables(PatternMatrix p, List<DataType> types, 
            List<LLVMValueRef> selectItems, List<Tuple<LLVMBasicBlockRef, int>> caseBlockPairs)
        {
            var scopes = new List<Dictionary<string, GeneratorSymbol>>();

            int r = 0;
            foreach (var cbp in caseBlockPairs)
            {               
                scopes.Add(new Dictionary<string, GeneratorSymbol>());

                for (int c = 0; c < p.Columns; c++)
                {
                    var pVar = p.GetElement(r, c);
                    LLVM.PositionBuilderAtEnd(_builder, cbp.Item1);

                    if (pVar.eType == PMatElementType.NAME)
                    {
                        if (_isReferenceType(types[c]))
                            scopes[r].Add(pVar.eName, new GeneratorSymbol(selectItems[c]));
                        else
                        {
                            var varRef = LLVM.BuildAlloca(_builder, selectItems[c].TypeOf(), pVar.eName);
                            LLVM.SetAlignment(varRef, _alignOf(types[c]));

                            LLVM.BuildStore(_builder, selectItems[c], varRef);

                            scopes[r].Add(pVar.eName, new GeneratorSymbol(varRef, true));
                        }
                    }
                }

                r += cbp.Item2;
            }        

            return scopes;
        }

        // -------------------------------
        // PATTERN MATCH/SELECT GENERATORS
        // -------------------------------

        private bool _generatePatternMatch(ITypeNode selectExprNode, List<TreeNode> caseNodes,
            List<LLVMBasicBlockRef> caseBlocks, LLVMBasicBlockRef defaultBlock, out List<Dictionary<string, GeneratorSymbol>> caseScopes)
        {
            var selectExpr = _generateExpr(selectExprNode);
            bool noDefault;

            var caseExprs = new List<ExprNode>();
            var columnCaseBlocks = new List<LLVMBasicBlockRef>();
            var caseBlockPairs = new List<Tuple<LLVMBasicBlockRef, int>>();

            for (int i = 0; i < caseNodes.Count; i++)
            {
                caseBlockPairs.Add(new Tuple<LLVMBasicBlockRef, int>(caseBlocks[i], caseNodes[i].Nodes.Count));

                foreach (var expr in caseNodes[i].Nodes.Select(x => ((ExprNode)x)))
                {
                    if (expr.Name == "ResultExpr")
                        break;

                    caseExprs.Add(expr);
                    columnCaseBlocks.Add(caseBlocks[i]);
                }
            }           

            if (selectExprNode.Type is TupleType tt)
            {
                var patternMatrix = _constructTuplePatternMatrix(tt.Types, caseExprs);

                var selectItems = new List<LLVMValueRef>();
                for (int i = 0; i < tt.Types.Count; i++)
                    selectItems.Add(_getLLVMStructMember(selectExpr, i, tt.Types[i], $"root_pattern_elem{i}"));

                noDefault = _generatePatternBranch(patternMatrix, tt.Types, selectItems, 0,
                    Enumerable.Range(0, caseExprs.Count), columnCaseBlocks, defaultBlock);

                caseScopes = _getCaseVariables(patternMatrix, tt.Types, selectItems, caseBlockPairs);
            }
            else if (selectExprNode.Type is CustomInstance ci)
            {
                noDefault = true;
                caseScopes = new List<Dictionary<string, GeneratorSymbol>>(caseBlocks.Count);

                var cVal = LLVM.BuildStructGEP(_builder, selectExpr, 1, "root_cval_elem_ptr_tmp");
                cVal = LLVM.BuildLoad(_builder, cVal, "root_cval_tmp");

                var instanceSwitchDefault = LLVM.AppendBasicBlock(_currFunctionRef, "type_class_instance_switch_default");
                var switchStmt = LLVM.BuildSwitch(_builder, cVal, instanceSwitchDefault, (uint)ci.Parent.Instances.Count - 1);
                for (int i = 0; i < ci.Parent.Instances.Count; i++)
                {
                    var instance = ci.Parent.Instances[i];
                    var validCaseExprs = caseExprs
                        .Select((x, k) => new { Value = x, Ndx = k})
                        .Where(x => x.Value.Type.Equals(instance))
                        .ToList();

                    if (validCaseExprs.Count == 0)
                        continue;

                    if (i == ci.Parent.Instances.Count - 1)
                        LLVM.PositionBuilderAtEnd(_builder, instanceSwitchDefault);
                    else
                    {
                        var switchOnVal = LLVM.ConstInt(LLVM.Int16Type(), (ulong)i, new LLVMBool(0));
                        var instanceCase = LLVM.AppendBasicBlock(_currFunctionRef, $"type_class_instance{i}_case");
                        switchStmt.AddCase(switchOnVal, instanceCase);
                        LLVM.PositionBuilderAtEnd(_builder, instanceCase);
                    }
                                      
                    var selectItems = _getTypeClassValues(selectExpr, instance);                    

                    List<DataType> instanceTypes;
                    if (instance is CustomAlias ca)
                        instanceTypes = new List<DataType> { ca.Type };
                    else
                        instanceTypes = ((CustomNewType)instance).Values;

                    var patternMatrix = _constructTypeClassPatternMatrix(instanceTypes, validCaseExprs.Select(x => x.Value).ToList());

                    noDefault &= _generatePatternBranch(patternMatrix, instanceTypes, selectItems, 0,
                         Enumerable.Range(0, validCaseExprs.Count), validCaseExprs.Select(x => columnCaseBlocks[x.Ndx]).ToList(), 
                         defaultBlock);

                    var validCaseBlockPairs = new List<Tuple<LLVMBasicBlockRef, int>>();

                    using (var e1 = caseBlockPairs.GetEnumerator())
                    using (var e2 = validCaseExprs.GetEnumerator())
                    {
                        e1.MoveNext();
                        e2.MoveNext();

                        int k = 0, s = 0;
                        for (int n = 0; n < columnCaseBlocks.Count; i++)
                        {
                            if (k++ == e1.Current.Item2)
                            {
                                if (s > 0)
                                    validCaseBlockPairs.Add(new Tuple<LLVMBasicBlockRef, int>(e1.Current.Item1, s));

                                s = 0;
                                k = 0;
                                e1.MoveNext();
                            }

                            if (n == e2.Current.Ndx)
                            {
                                s++;

                                if (!e2.MoveNext())
                                {
                                    validCaseBlockPairs.Add(new Tuple<LLVMBasicBlockRef, int>(e1.Current.Item1, s));
                                    break;
                                }
                            }
                        }
                    }

                    var currCaseScopes = _getCaseVariables(patternMatrix, instanceTypes, selectItems, validCaseBlockPairs);

                    using (var e1 = caseBlockPairs.GetEnumerator())
                    using (var e2 = validCaseExprs.GetEnumerator())
                    using (var e3 = currCaseScopes.GetEnumerator())
                    {
                        int k = 0, csNdx = 0;
                        for (int n = 0; n < columnCaseBlocks.Count; n++)
                        {
                            if (k++ == e1.Current.Item2)
                            {
                                k = 0;
                                csNdx++;
                                e1.MoveNext();
                            }

                            if (n == e2.Current.Ndx)
                            {
                                caseScopes[csNdx] = e3.Current;

                                if (e2.MoveNext())
                                    break;

                                e3.MoveNext();
                            }
                        }
                    }                       
                }
            }
            // NEVER REACHED
            else
            {
                caseScopes = null;
                noDefault = true;
            }

            _moveBlocksToEnd(caseBlocks, defaultBlock);

            return noDefault;
        }

        private bool _generateSelectLogic(DataType selectType, ITypeNode selectExprNode, 
            List<ITypeNode> caseNodes, List<LLVMBasicBlockRef> caseBlocks, LLVMBasicBlockRef defaultBlock,
            out List<Dictionary<string, GeneratorSymbol>> caseScopes)
        {
            bool noDefault;
            if (_isPatternType(selectType))
                noDefault = _generatePatternMatch(selectExprNode, caseNodes
                    .Where(x => x.Name == "Case").Select(x => (TreeNode)x).ToList(),
                    caseBlocks, defaultBlock, out caseScopes);
            // is standard switch case (no pattern matching)
            else
            {
                var selectExpr = _generateExpr(selectExprNode);

                bool canSwitch;
                if (Hashable(selectType))
                {
                    if (_needsHash(selectType))
                        selectExpr = _getHash(selectExpr, selectType).Item1;

                    canSwitch = true;
                }
                else
                    canSwitch = false;

                var caseValPairs = caseNodes
                    .Take(caseBlocks.Count)
                    .SelectMany(x => ((TreeNode)x).Nodes)
                    .Select(x => _getSwitchableValue(x, selectType));

                canSwitch &= caseValPairs.All(x => x.Item2);

                _switchBetween(selectExpr, caseValPairs.Select(x => x.Item1).ToList(),
                    caseBlocks, defaultBlock, selectType, canSwitch);

                noDefault = true;
                caseScopes = new List<Dictionary<string, GeneratorSymbol>>();
            }

            return noDefault;
        }
    }
}
