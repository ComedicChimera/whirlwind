using System;
using System.Collections.Generic;
using System.Linq;

using LLVMSharp;

using Whirlwind.Semantic;
using Whirlwind.Types;

using static Whirlwind.Semantic.Checker.Checker;

namespace Whirlwind.Generation
{
    partial class Generator
    {
        // -----------------------
        // BINOP BUILDER DELEGATES
        // -----------------------

        private delegate LLVMValueRef BinopBuilder(LLVMBuilderRef builder, LLVMValueRef vRef, LLVMValueRef rRef, string name);

        private delegate LLVMValueRef IntCompareBinopBuilder(LLVMBuilderRef builder, LLVMIntPredicate pred,
            LLVMValueRef vRef, LLVMValueRef rRef, string name);
        private delegate LLVMValueRef RealCompareBinopBuilder(LLVMBuilderRef builder, LLVMRealPredicate pred,
            LLVMValueRef vRef, LLVMValueRef rRef, string name);

        private delegate BinopBuilder NumericBinopBuilderFactory(int category);

        // -------------------------
        // MAIN EXPRESSION GENERATOR 
        // -------------------------

        private LLVMValueRef _generateExpr(ITypeNode expr, bool mutableExpr=false)
        {
            if (expr is ValueNode vnode)
                return _generateExprValue(vnode);
            // hopefully this is ok
            else if (expr is IdentifierNode inode)
            {
                var genSym = _getNamedValue(inode.IdName);

                if (mutableExpr)
                    return genSym.Vref;

                return _loadValue(genSym);
            }
            else if (expr is ConstexprNode cnode)
                return _generateExpr(cnode.ConstValue);
            // only other option is expr node
            else
            {
                var enode = (ExprNode)expr;

                switch (expr.Name)
                {
                    case "Array":
                        return _generateArrayLiteral(enode);
                    case "GetMember":
                        {
                            string memberName = ((IdentifierNode)enode.Nodes[1]).IdName;

                            if (enode.Nodes[0].Type is StructType st)
                            {
                                int memberNdx = st.Members.Keys.ToList().IndexOf(memberName);

                                var memberPtr = LLVM.BuildStructGEP(_builder, _generateExpr(enode.Nodes[0]), (uint)memberNdx, "struct_gep_tmp");

                                if (mutableExpr)
                                    return memberPtr;
                                else
                                    return LLVM.BuildLoad(_builder, memberPtr, "struct_member." + memberName);
                            }
                            else if (enode.Nodes[0].Type is InterfaceType it)
                            {
                                string rootName = _getLookupName(enode.Nodes[0]);
                                var baseInterf = _generateExpr(enode.Nodes[0]);

                                if (_isVTableMethod(it, memberName))
                                {

                                    var vtable = LLVM.BuildStructGEP(_builder, baseInterf, 1, "vtable_tmp");

                                    int vtableNdx = _getVTableNdx(it, memberName);

                                    var gepRes = LLVM.BuildStructGEP(_builder, vtable,
                                        (uint)vtableNdx, "vtable_gep_tmp");

                                    return _boxFunction(gepRes, baseInterf);
                                }
                                else
                                    return _boxFunction(_globalScope[rootName + ".interf." + memberName].Vref, baseInterf);
                            }
                        }
                        break;
                    case "GetTIMethod":
                        {
                            string rootName = _getLookupName(enode.Nodes[0]);
                            var baseInterf = _generateExpr(enode.Nodes[0]);
                            string memberName = ((IdentifierNode)enode.Nodes[1]).IdName;

                            return _boxFunction(_globalScope[rootName + ".interf." + memberName].Vref, baseInterf);
                        }   
                    case "CreateGeneric":
                        {
                            var generateName = _getLookupName(enode.Nodes[0]);                           

                            // structs are handled in the CallConstructor handler
                            // and interfaces are never used this way
                            return _globalScope[generateName].Vref;
                        }
                    case "StaticGet":
                        {
                            var rootType = enode.Nodes[0].Type;

                            if (rootType is CustomType ct)
                            {
                                // the CallTCConstructor handler will compile with values as necessary (overrides this logic)
                                var idPos = ct.Instances.IndexOf((CustomInstance)enode.Type);

                                // interpret as pure enum (pure alises don't make it this far)
                                return LLVM.ConstInt(LLVM.Int16Type(), (ulong)idPos, new LLVMBool(0));
                            }
                            else if (rootType is PackageType pt)
                            {
                                // TODO: package stuff
                            }
                        }
                        break;
                    case "Call":
                        return _generateCall(enode, (FunctionType)enode.Nodes[0].Type, _generateExpr(enode.Nodes[0]));
                    case "CallOverload":
                        {
                            var fg = (FunctionGroup)enode.Nodes[0];
                            fg.GetFunction(_createArgsList(enode), out FunctionType ft);

                            string name = _getLookupName(enode.Nodes[0]);
                            name += "." + string.Join(",", ft.Parameters.Select(x => x.DataType.LLVMName()));

                            return _generateCall(enode, ft, _loadGlobalValue(name));
                        }
                    case "CallConstructor":
                        return _generateCallConstructor(enode);
                    case "CallTCConstructor":
                        return _generateCallTCConstructor(enode);
                    case "From":
                        return _generateFromExpr(enode);
                    case "TypeCast":
                        if (mutableExpr && !_isReferenceType(enode.Type))
                            return _cast(_generateExpr(enode.Nodes[0], mutableExpr), enode.Nodes[0].Type, new PointerType(enode.Type, false));

                        return _cast(_generateExpr(enode.Nodes[0], mutableExpr), enode.Nodes[0].Type, enode.Type);
                    case "Add":
                        {
                            if (expr.Type is ArrayType at)
                            {

                            }
                            else if (expr.Type is ListType lt)
                            {

                            }
                            else if (expr.Type is SimpleType st && st.Type == SimpleType.SimpleClassifier.STRING)
                            {

                            }
                            else
                            {
                                return _buildNumericBinop(category =>
                                {
                                    if (category == 2)
                                        return LLVM.BuildFAdd;
                                    else
                                        return LLVM.BuildAdd;
                                }, enode);
                            }
                        }
                        break;
                    case "Sub":
                        return _buildNumericBinop(category =>
                        {
                            if (category == 2)
                                return LLVM.BuildFSub;
                            else
                                return LLVM.BuildSub;
                        }, enode);                     
                    case "Mul":
                        return _buildNumericBinop(category =>
                        {
                            if (category == 2)
                                return LLVM.BuildFMul;
                            else
                                return LLVM.BuildMul;
                        }, enode);
                    // TODO: add NaN checking to both div and floordiv
                    case "Div":
                        return _buildNumericBinop(category =>
                        {
                            if (category == 2)
                                return LLVM.BuildFDiv;
                            else if (category == 1)
                                return LLVM.BuildUDiv;
                            else
                                return LLVM.BuildSDiv;
                        }, enode);
                    // TODO: see note on NaN checking
                    // TODO: make sure floor div is compatable with overload capability
                    case "Floordiv":
                        {
                            var result = _buildNumericBinop(category =>
                            {
                                if (category == 2)
                                    return LLVM.BuildFDiv;
                                else if (category == 1)
                                    return LLVM.BuildUDiv;
                                else
                                    return LLVM.BuildSDiv;
                            }, enode);

                            var commonType = _getCommonType(enode);
                            if (!enode.Type.Equals(commonType))
                                return _cast(result, commonType, enode.Type);

                            return result;
                        }
                    // TODO: add NaN checking
                    case "Mod":                        
                        return _buildNumericBinop(category =>
                        {
                            if (category == 2)
                                return LLVM.BuildFRem;
                            else if (category == 1)
                                return LLVM.BuildURem;
                            else
                                return LLVM.BuildSRem;
                        }, enode);
                    // TODO: add power operator implementation
                    case "LShift":
                        return _buildBinop(LLVM.BuildShl, enode, _getCommonType(enode));
                    // TODO: test binary right shift operation to make sure it is on signed integral types
                    case "RShift":
                        {
                            if (expr.Type is SimpleType st && !st.Unsigned)
                                return _buildBinop(LLVM.BuildAShr, enode, _getCommonType(enode));

                            return _buildBinop(LLVM.BuildLShr, enode, _getCommonType(enode));
                        }
                    case "Eq":
                        return _buildNumericBinop(category =>
                        {
                            if (category == 2)
                                return _buildCompareBinop(LLVM.BuildFCmp, LLVMRealPredicate.LLVMRealOEQ);

                            return _buildCompareBinop(LLVM.BuildICmp, LLVMIntPredicate.LLVMIntEQ);

                        }, enode);
                    case "Neq":
                        return _buildNumericBinop(category =>
                        {
                            if (category == 2)
                                return _buildCompareBinop(LLVM.BuildFCmp, LLVMRealPredicate.LLVMRealONE);

                            return _buildCompareBinop(LLVM.BuildICmp, LLVMIntPredicate.LLVMIntNE);

                        }, enode);
                    case "Gt":
                        return _buildNumericBinop(category =>
                        {
                            if (category == 2)
                                return _buildCompareBinop(LLVM.BuildFCmp, LLVMRealPredicate.LLVMRealOGT);
                            else if (category == 1)
                                return _buildCompareBinop(LLVM.BuildICmp, LLVMIntPredicate.LLVMIntUGT);

                            return _buildCompareBinop(LLVM.BuildICmp, LLVMIntPredicate.LLVMIntSGT);
                        }, enode);
                    case "Lt":
                        return _buildNumericBinop(category =>
                        {
                            if (category == 2)
                                return _buildCompareBinop(LLVM.BuildFCmp, LLVMRealPredicate.LLVMRealOLT);
                            else if (category == 1)
                                return _buildCompareBinop(LLVM.BuildICmp, LLVMIntPredicate.LLVMIntULT);

                            return _buildCompareBinop(LLVM.BuildICmp, LLVMIntPredicate.LLVMIntSLT);
                        }, enode);
                    case "GtEq":
                        return _buildNumericBinop(category =>
                        {
                            if (category == 2)
                                return _buildCompareBinop(LLVM.BuildFCmp, LLVMRealPredicate.LLVMRealOGE);
                            else if (category == 1)
                                return _buildCompareBinop(LLVM.BuildICmp, LLVMIntPredicate.LLVMIntUGE);

                            return _buildCompareBinop(LLVM.BuildICmp, LLVMIntPredicate.LLVMIntSGE);
                        }, enode);
                    case "LtEq":
                        return _buildNumericBinop(category =>
                        {
                            if (category == 2)
                                return _buildCompareBinop(LLVM.BuildFCmp, LLVMRealPredicate.LLVMRealOLE);
                            else if (category == 1)
                                return _buildCompareBinop(LLVM.BuildICmp, LLVMIntPredicate.LLVMIntULE);

                            return _buildCompareBinop(LLVM.BuildICmp, LLVMIntPredicate.LLVMIntSLE);
                        }, enode);
                    case "And":
                        return _buildBinop(LLVM.BuildAnd, enode, _getCommonType(enode));
                    case "Or":
                        return _buildBinop(LLVM.BuildOr, enode, _getCommonType(enode));
                    case "Xor":
                        return _buildBinop(LLVM.BuildXor, enode, _getCommonType(enode));
                    // TODO: check for overloads on complement, not, and change sign
                    case "Complement":
                        return LLVM.BuildXor(_builder, _generateExpr(enode.Nodes[0]),
                            _getOne(enode.Type), "compl_tmp");
                    case "Not":
                        return LLVM.BuildXor(_builder, _generateExpr(enode.Nodes[0]),
                            LLVM.ConstInt(LLVM.Int1Type(), 1, new LLVMBool(0)), "not_tmp");
                    case "ChangeSign":
                        {
                            if (_getSimpleClass((SimpleType)enode.Type) == 0)
                                return LLVM.BuildSub(_builder, _generateExprValue(new ValueNode("Literal", enode.Type, "0")),
                                    _generateExpr(enode.Nodes[0]), "cs_tmp");
                            else
                                return LLVM.BuildFNeg(_builder, _generateExpr(enode), "cs_tmp");
                        }
                    case "InlineComparison":
                        {
                            List<LLVMValueRef> elements = enode.Nodes
                                .Select(x => _generateExpr(x)).ToList();

                            // node layout: then, if, else

                            if (enode.Type.Equals(enode.Nodes[0].Type) && !enode.Type.Equals(enode.Nodes[2]))
                                elements[2] = _cast(elements[2], enode.Nodes[2].Type, enode.Nodes[0].Type);
                            else if (enode.Type.Equals(enode.Nodes[2].Type) && !enode.Type.Equals(enode.Nodes[0]))
                                elements[0] = _cast(elements[0], enode.Nodes[0].Type, enode.Nodes[2].Type);

                            return LLVM.BuildSelect(_builder, elements[1], elements[0], elements[2], "inline_cmp_res");
                        }
                    case "Then":
                        {
                            var valRes = _generateExpr(enode.Nodes[0]);
                            _scopes.Last()["value_tmp"] = new GeneratorSymbol(valRes);

                            return _generateExpr(enode.Nodes[1]);
                        }
                    case "Indirect":
                        {
                            // nullify indirect if it is just referencing a variable (just use varRef)
                            if (enode.Nodes[0] is IdentifierNode idNode)
                            {
                                var genSym = _getNamedValue(idNode.IdName);

                                if (genSym.IsPointer)
                                    return genSym.Vref;
                            }

                            var ptr = LLVM.BuildAlloca(_builder, _convertType(enode.Nodes[0].Type), "indirect_tmp");
                            LLVM.BuildStore(_builder, _generateExpr(enode.Nodes[0]), ptr);

                            return ptr;
                        }
                    case "Dereference":
                        // deref compiles as no-op if expression is supposed to mutable (at top level)
                        if (mutableExpr)
                            return _generateExpr(enode.Nodes[0]);

                        return LLVM.BuildLoad(_builder, _generateExpr(enode.Nodes[0]), "deref_tmp");
                    // TODO: check for overloads on increment and decrement operators
                    case "Increment":
                        {
                            var incRes = LLVM.BuildAdd(_builder,
                                _generateExpr(enode.Nodes[0]),
                                _getOne(enode.Type),
                                "increm_tmp");

                            LLVM.BuildStore(_builder, incRes, _generateExpr(enode.Nodes[0], true));

                            return incRes;
                        }
                    case "Decrement":
                        {
                            var decRes = LLVM.BuildSub(_builder,
                                _generateExpr(enode.Nodes[0]),
                                _getOne(enode.Type),
                                "decrem_tmp");

                            LLVM.BuildStore(_builder, decRes, _generateExpr(enode.Nodes[0], true));

                            return decRes;
                        }
                    case "PostfixIncrement":
                        {
                            var oriVal = _generateExpr(enode.Nodes[0]);

                            var incRes = LLVM.BuildAdd(_builder,
                                oriVal,
                                _getOne(enode.Type),
                                "p_increm_tmp");

                            LLVM.BuildStore(_builder, incRes, _generateExpr(enode.Nodes[0], true));

                            return oriVal;
                        }
                    case "PostfixDecrement":
                        {
                            var oriVal = _generateExpr(enode.Nodes[0]);

                            var decRes = LLVM.BuildSub(_builder,
                                oriVal,
                                _getOne(enode.Type),
                                "p_decrem_tmp");

                            LLVM.BuildStore(_builder, decRes, _generateExpr(enode.Nodes[0], true));

                            return oriVal;
                        }
                }
            }

            return _ignoreValueRef();
        }

        // ---------------------------------
        // BINOP EXPRESSION GENERATION UTILS
        // ---------------------------------

        private BinopBuilder _buildCompareBinop(IntCompareBinopBuilder cbb, LLVMIntPredicate predicate)
        {
            return (b, lv, rv, name) => cbb(b, predicate, lv, rv, name);
        }

        private BinopBuilder _buildCompareBinop(RealCompareBinopBuilder rcbb, LLVMRealPredicate predicate)
        {
            return (b, lv, rv, name) => rcbb(b, predicate, lv, rv, name);
        }

        private LLVMValueRef _buildNumericBinop(NumericBinopBuilderFactory nbbfactory, ExprNode node)
        {
            // TODO: check for overloads

            var commonType = _getCommonType(node);
            int instrCat = 0;

            // we assume the common type is a simple type if it is being interpreted as numeric
            if (((SimpleType)commonType).Unsigned)
                instrCat = 1;
            else if (new[] { SimpleType.SimpleClassifier.FLOAT, SimpleType.SimpleClassifier.DOUBLE}
                .Contains(((SimpleType)commonType).Type))
            {
                instrCat = 2;
            }

            var binopBuilder = nbbfactory(instrCat);

            return _buildBinop(binopBuilder, node, commonType, true);
        }

        private LLVMValueRef _buildBinop(BinopBuilder bbuilder, ExprNode node, DataType commonType, bool noOverloads = false)
        {
            var operands = _buildOperands(node.Nodes, commonType);

            var leftOperand = operands[0];

            foreach (var rightOperand in operands.Skip(1))
            {
                // TODO: check for overloads

                leftOperand = bbuilder(_builder, leftOperand, rightOperand, node.Name.ToLower() + "_tmp");
            }

            return leftOperand;
        }

        private DataType _getCommonType(ExprNode node)
        {
            var commonType = node.Nodes[0].Type;

            foreach (var item in node.Nodes)
            {
                if (!commonType.Coerce(item.Type) && item.Type.Coerce(commonType))
                    commonType = item.Type;
            }

            return commonType;
        }

        private List<LLVMValueRef> _buildOperands(List<ITypeNode> nodes, DataType commonType)
        {
            var results = new List<LLVMValueRef>();

            foreach (var node in nodes)
            {
                var g = _generateExpr(node);

                if (!commonType.Equals(node.Type))
                    results.Add(_cast(g, node.Type, commonType));
                else
                    results.Add(g);
            }

            return results;
        }

        // ------------------
        // GENERATION HELPERS
        // ------------------

        // Note: generates as many overloads as it can of a single operator; returns depth it reached
        private int _generateOperatorOverload(ExprNode expr, int startDepth, out LLVMValueRef res)
        {
            if (expr.Nodes.Count - startDepth == 1)
            {
                var opType = expr.Nodes[startDepth].Type;
                var opMethod = _convertOverloadTreeToOpMethod(expr.Name);

                if (HasOverload(opType, opMethod, out DataType _))
                {
                    /*res = LLVM.BuildCall(_builder, _getNamedValue(opType.ToString() + ".operator." + opMethod),
                        _generateExpr();*/
                }

                res = _ignoreValueRef();
                return startDepth;
            }
            else
            {

            }


            res = _ignoreValueRef();
            return 0;
        } 

        private string _convertOverloadTreeToOpMethod(string name)
        {
            switch (name)
            {
                case "Add":
                    return "__+__";
                case "Neg":
                case "Sub":
                    return "__-__";
                case "Mul":
                    return "__*__";
                case "Div":
                    return "__/__";
                case "Floordiv":
                    return "__~/__";
                // TODO: add other overload conversions
                default:
                    return "__~^__";
            }
        }

        private LLVMValueRef _generateArrayLiteral(ExprNode enode)
        {
            var elemType = ((ArrayType)enode.Type).ElementType;

            var llvmElementType = _convertType(elemType);

            var arrLit = LLVM.BuildArrayAlloca(
                _builder,
                llvmElementType,
                LLVM.ConstInt(LLVM.Int32Type(), (ulong)enode.Nodes.Count, new LLVMBool(0)),
                "array_lit");

            uint i = 0;
            foreach (var item in enode.Nodes)
            {
                var vRef = _generateExpr(item);

                if (!elemType.Equals(item.Type))
                    vRef = _cast(vRef, item.Type, elemType);

                var elemPtr = LLVM.BuildGEP(_builder, arrLit,
                    new[] {
                        LLVM.ConstInt(LLVM.Int32Type(), 0, new LLVMBool(0)),
                        LLVM.ConstInt(LLVM.Int32Type(), i, new LLVMBool(0))
                    },
                    "elem_ptr"
                    );

                LLVM.BuildStore(_builder, vRef, elemPtr);

                i++;
            }

            var arrPtr = LLVM.BuildInBoundsGEP(_builder, arrLit,
                new[] { LLVM.ConstInt(LLVM.Int32Type(), 0, new LLVMBool(0)) },
                "arr_ptr");

            // create struct first!
            return arrPtr;
        }

        private LLVMValueRef _generateFromExpr(ExprNode enode)
        {
            var rootType = (CustomNewType)enode.Nodes[0].Type;

            var gepRes = LLVM.BuildStructGEP(
                _builder,
                _generateExpr(enode.Nodes[0]),
                1,
                "from_tmp");

            if (rootType.Values.Count > 1)
            {
                var tupleElements = new LLVMValueRef[rootType.Values.Count];

                for (int i = 0; i < rootType.Values.Count; i++)
                {
                    var elemGepRes = LLVM.BuildInBoundsGEP(
                        _builder,
                        gepRes,
                        new[] { LLVM.ConstInt(LLVM.Int16Type(), (ulong)i, new LLVMBool(0)) },
                        "from_elem_gep_tmp"
                        );

                    var elemCastRes = LLVM.BuildBitCast(
                        _builder,
                        elemGepRes,
                        LLVM.PointerType(_convertType(((TupleType)enode.Type).Types[i]), 0),
                        "from_elem_cast_tmp"
                        );

                    tupleElements[i] = LLVM.BuildLoad(
                        _builder,
                        elemCastRes,
                        "from_elem_deref_tmp"
                        );
                }

                return LLVM.ConstStructInContext(_ctx, tupleElements, true);
            }
            else
            {
                var castRes = LLVM.BuildBitCast(
                    _builder,
                    gepRes,
                    LLVM.PointerType(_convertType(enode.Type), 0),
                    "from_cast_tmp"
                    );

                return LLVM.BuildLoad(
                    _builder,
                    castRes,
                    "from_deref_tmp"
                    );
            }
        }
        
        private LLVMValueRef _generateCall(ExprNode enode, FunctionType ft, LLVMValueRef genFn)
        {
            var argArray = _buildArgArray(ft, enode);

            LLVMValueRef rtPtr = _ignoreValueRef();
            bool returnCallResult = true;

            if (_isReferenceType(ft.ReturnType))
            {
                var transformedArgArray = new LLVMValueRef[argArray.Length + 1];

                rtPtr = LLVM.BuildAlloca(_builder, _convertType(ft.ReturnType), "rtptr_tmp");

                transformedArgArray[0] = rtPtr;
                argArray.CopyTo(transformedArgArray, 1);

                argArray = transformedArgArray;

                returnCallResult = false;
            }

            if (ft.IsBoxed)
            {
                var fPtr = LLVM.BuildStructGEP(_builder, genFn, 0, "fptr_tmp");
                var statePtr = LLVM.BuildStructGEP(_builder, genFn, 1, "state_ptr_tmp");

                if (ft.IsMethod)
                    statePtr = LLVM.BuildBitCast(_builder, statePtr, fPtr.TypeOf().GetParamTypes()[0], "this_ptr_tmp");

                var boxedFnArgArray = new LLVMValueRef[argArray.Length + 1];
                boxedFnArgArray[0] = statePtr;

                argArray.CopyTo(boxedFnArgArray, 1);

                var callResult = LLVM.BuildCall(_builder, fPtr, boxedFnArgArray, "call_boxed_tmp");
                if (returnCallResult)
                    return callResult;
            }
            else
            {
                var callResult = LLVM.BuildCall(_builder, genFn, argArray, "call_tmp");
                if (returnCallResult)
                    return callResult;
            }

            return rtPtr;
        }

        private LLVMValueRef _generateCallConstructor(ExprNode enode)
        {
            var newStruct = LLVM.BuildAlloca(_builder, _convertType(enode.Type), "nstruct_tmp");

            var st = (StructType)enode.Nodes[0].Type;
            string lookupName = st.Name;

            // check for _$initMembers
            string initMembersLookup = lookupName + "._$initMembers";
            if (_globalScope.ContainsKey(initMembersLookup))
            {
                LLVM.BuildCall(_builder, _globalScope[initMembersLookup].Vref,
                    new[] { newStruct }, "");
            }

            string constructorLookup = lookupName + ".constructor";

            FunctionType constr;
            if (st.HasMultipleConstructors())
            {
                var args = _createArgsList(enode);

                st.GetConstructor(args, out constr);

                constructorLookup += "." + string.Join(",", constr.Parameters.Select(x => x.DataType.LLVMName()));
            }
            else
                constr = st.GetFirstConstructor();

            var baseArgsArray = _buildArgArray(constr, enode);
            LLVM.BuildCall(_builder, _globalScope[constructorLookup].Vref, _insertFront(baseArgsArray, newStruct), "");

            return newStruct;
        }

        private LLVMValueRef _generateCallTCConstructor(ExprNode enode)
        {
            var cnt = (CustomNewType)enode.Nodes[0].Type;
            LLVMValueRef tcRef;

            if (cnt.Values.Count == 1)
            {
                var valPtr = LLVM.BuildAlloca(_builder, _convertType(cnt.Values[0]), "tc_valptr_tmp");                
                LLVM.BuildStore(_builder, _generateExpr(enode.Nodes[1]), valPtr);

                valPtr = LLVM.BuildBitCast(_builder, valPtr, LLVM.PointerType(LLVM.Int8Type(), 0), "tc_valptr_i8_tmp");

                tcRef = LLVM.BuildAlloca(_builder, LLVM.StructType(
                    new[] { LLVM.Int32Type(), valPtr.TypeOf() }, false), "tc_tmp");

                var firstElem = LLVM.BuildBitCast(_builder, tcRef, LLVM.PointerType(LLVM.Int16Type(), 0), "tc_enum_ptr_tmp");
                LLVM.BuildStore(_builder, firstElem, 
                    LLVM.ConstInt(LLVM.Int16Type(), (ulong)cnt.Parent.Instances.IndexOf(cnt), new LLVMBool(0)));

                var valPtrElem = LLVM.BuildStructGEP(_builder, tcRef, 1, "tc_val_ptr_tmp");
                LLVM.BuildStore(_builder, valPtr, valPtrElem);
            }
            else
            {
                var valArrPtr = LLVM.BuildArrayAlloca(
                    _builder,
                    LLVM.PointerType(LLVM.Int8Type(), 0),
                    LLVM.ConstInt(LLVM.Int32Type(), (ulong)cnt.Values.Count, new LLVMBool(0)),
                    "tc_valarr_tmp"
                    );

                for (int i = 0; i < cnt.Values.Count; i++)
                {
                    var valPtr = LLVM.BuildAlloca(_builder, _convertType(cnt.Values[i]), "tc_elem_valptr_tmp");
                    LLVM.BuildStore(_builder, _generateExpr(enode.Nodes[i + 1]), valPtr);

                    var valArrElemPtr = LLVM.BuildGEP(_builder, valArrPtr,
                        new[] {
                            LLVM.ConstInt(LLVM.Int32Type(), (ulong)i, new LLVMBool(0))
                        }, "tc_valarr_elem_tmp");

                    var i8ValPtr = LLVM.BuildBitCast(_builder, valPtr,
                        LLVM.PointerType(LLVM.Int8Type(), 0), "tc_valarr_elem_val_tmp");

                    LLVM.BuildStore(_builder, i8ValPtr, valArrElemPtr);
                }

                tcRef = LLVM.BuildAlloca(_builder, LLVM.StructType(
                    new[] { LLVM.Int32Type(), valArrPtr.TypeOf() }, false), "tc_tmp");

                var firstElem = LLVM.BuildBitCast(_builder, tcRef, LLVM.PointerType(LLVM.Int16Type(), 0), "tc_enum_ptr_tmp");
                LLVM.BuildStore(_builder, firstElem,
                    LLVM.ConstInt(LLVM.Int16Type(), (ulong)cnt.Parent.Instances.IndexOf(cnt), new LLVMBool(0)));

                var valArrPtrElem = LLVM.BuildStructGEP(_builder, tcRef, 1, "tc_valarr_ptr_tmp");
                LLVM.BuildStore(_builder, valArrPtr, valArrPtrElem);
            }

            return tcRef;
        }

        // TODO: handle indefinites
        private LLVMValueRef[] _buildArgArray(FunctionType ft, ExprNode enode)
        {
            var argArray = new LLVMValueRef[ft.Parameters.Count];

            int i = 1;
            for (; i < enode.Nodes.Count; i++)
            {
                var item = enode.Nodes[i];

                if (item.Name == "NamedArgument")
                    break;

                argArray[i - 1] = _copy(_generateExpr(item), item.Type);
            }

            if (i == enode.Nodes.Count - 1)
            {
                // encountered named values
                for (; i < enode.Nodes.Count; i++)
                {
                    var item = (ExprNode)enode.Nodes[i];
                    string itemName = ((IdentifierNode)item.Nodes[0]).IdName;
                    int ftNdx = ft.Parameters
                        .Select((x, ndx) => new { Param = x, Ndx = ndx })
                        .Where(x => x.Param.Name == itemName)
                        .First().Ndx;

                    argArray[ftNdx] = _copy(_generateExpr(item.Nodes[1]), item.Nodes[1].Type);
                }
            }

            return argArray;
        }

        private LLVMValueRef _getOne(DataType dt)
        {
            byte sClass = _getSimpleClass((SimpleType)dt);

            if (sClass == 0)
                return LLVM.ConstInt(_convertType(dt), 1, new LLVMBool(0));
            else
                return LLVM.ConstReal(_convertType(dt), 1);
        }

        private LLVMValueRef _boxFunction(LLVMValueRef fn, LLVMValueRef state)
        {
            var i8PtrType = LLVM.PointerType(LLVM.Int8Type(), 0);

            var i8StatePtr = LLVM.BuildBitCast(_builder, state, i8PtrType, "state_ptr_tmp");

            var boxedFunctionStruct = LLVM.BuildAlloca(_builder, LLVM.StructType(new[] { fn.TypeOf(), i8PtrType }, false), "boxed_fn_tmp");

            var fnElem = LLVM.BuildStructGEP(_builder, boxedFunctionStruct, 0, "boxed_fn_fnptr_tmp");
            LLVM.BuildStore(_builder, fn, fnElem);

            var stateElem = LLVM.BuildStructGEP(_builder, boxedFunctionStruct, 1, "boxed_fn_stateptr_tmp");
            LLVM.BuildStore(_builder, state, stateElem);

            return boxedFunctionStruct;
        }

        private int _getVTableNdx(InterfaceType it, string name)
        {
            int vtableNdx = 0;
            foreach (var method in it.Methods)
            {
                if (!it.Implements.Any(x => x.GetFunction(name, out Symbol _)))
                    continue;

                if (method.Key.Name == name)
                    break;

                if (method.Key.DataType is GenericType gt)
                    vtableNdx += gt.Generates.Count;
                else
                    vtableNdx++;
            }

            return vtableNdx;
        }
    }
}
