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
            else if (expr is LLVMVRefNode vrnode)
                return vrnode.Vref;
            // only other option is expr node
            else
            {
                var enode = (ExprNode)expr;

                switch (expr.Name)
                {
                    case "Array":
                        return _generateArrayLiteral(enode);
                    case "Tuple":
                        return _generateTupleLiteral(enode.Nodes);
                    case "GetMember":
                        return _generateGetMember(enode, mutableExpr);
                    case "GetTIMethod":
                        {
                            // assumes not overloads and no generics (handled by other parts of the code)
                            string rootName = _getLookupName(enode.Nodes[0].Type);
                            var typeInterf = _generateExpr(enode.Nodes[0]);
                            string memberName = ((IdentifierNode)enode.Nodes[1]).IdName;

                            var interfType = enode.Nodes[0].Type.GetInterface();
                            var method = interfType.Methods.Single(x => x.Key.Name == memberName);

                            if (method.Value == MethodStatus.VIRTUAL)
                            {
                                var rootInterf = interfType.Implements.First(x => x.Methods.Contains(method));

                                rootName = _getLookupName(rootInterf);
                                typeInterf = _cast(typeInterf, enode.Nodes[0].Type, rootInterf);
                            }
                            else
                                typeInterf = _boxToInterf(typeInterf, enode.Nodes[0].Type); // standard up box (with no real vtable)

                            typeInterf = LLVM.BuildBitCast(_builder, typeInterf, _i8PtrType, "boxed_ti_i8ptr_tmp");

                            return _boxFunction(_globalScope[rootName + ".interf." + memberName].Vref, typeInterf);
                        }
                    // tuples are never mutable so we don't need to account for mutable expr case
                    case "GetTupleMember":
                        {
                            var tupleRoot = _generateExpr(enode.Nodes[0]);
                            var ndx = Int32.Parse(((ValueNode)enode.Nodes[1]).Value);

                            return _getLLVMStructMember(tupleRoot, ndx, ((TupleType)enode.Nodes[0]).Types[ndx], $"tuple_elem.{ndx}");
                        }
                    case "CreateGeneric":
                        return _generateCreateGeneric(enode);
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
                            else if (rootType is PackageType)
                            {
                                string lookupName = _getIdentifierName(enode);

                                if (mutableExpr)
                                    return _globalScope[lookupName].Vref;
                                else
                                    return _loadGlobalValue(lookupName);
                            }
                        }
                        break;
                    case "Call":
                        return _generateCall(enode, (FunctionType)enode.Nodes[0].Type, _generateExpr(enode.Nodes[0]));
                    case "CallOverload":
                        {
                            var root = enode.Nodes[0];

                            var fg = (FunctionGroup)root.Type;
                            fg.GetFunction(_createArgsList(enode), out FunctionType ft);

                            string overloadSuffix = string.Join(",", ft.Parameters.Select(x => x.DataType.LLVMName()));

                            LLVMValueRef llvmFn;
                            if (root.Name == "GetMember")
                            {
                                var interfGetMember = (ExprNode)root;

                                var interfNode = interfGetMember.Nodes[0];
                                var it = (InterfaceType)interfNode.Type;

                                var vtableNdx = _getVTableNdx(it, ((IdentifierNode)interfGetMember.Nodes[1]).IdName);
                                vtableNdx += fg.Functions.IndexOf(ft);

                                llvmFn = _generateVtableGet(_generateExpr(interfNode), vtableNdx);
                            }
                            else if (root.Name == "GetTIMethod")
                            {
                                var tiGetMember = (ExprNode)root;

                                string rootName = _getLookupName(tiGetMember.Nodes[0].Type);
                                var typeInterf = _generateExpr(tiGetMember.Nodes[0]);
                                string memberName = ((IdentifierNode)tiGetMember.Nodes[1]).IdName;

                                var interfType = tiGetMember.Nodes[0].Type.GetInterface();
                                var method = interfType.Methods.Single(x => x.Key.Name == memberName);

                                if (method.Value == MethodStatus.VIRTUAL)
                                {
                                    rootName = _getLookupName(interfType.Implements.First(x => x.Methods.Contains(method)));
                                    typeInterf = _cast(typeInterf, enode.Nodes[0].Type, interfType);
                                }
                                else
                                    typeInterf = _boxToInterf(typeInterf, tiGetMember.Nodes[0].Type); // standard up box (with no real vtable)

                                llvmFn = _boxFunction(_loadGlobalValue(rootName + ".interf." + memberName + "." + overloadSuffix), typeInterf);
                            }
                            else
                                llvmFn = _loadGlobalValue(fg.Name + "." + overloadSuffix);

                            return _generateCall(enode, ft, llvmFn);
                        }
                    case "CallGenericOverload":
                        {
                            var root = enode.Nodes[0];

                            var gg = (GenericGroup)root.Type;
                            gg.GetFunction(_createArgsList(enode), out FunctionType ft);

                            string overloadSuffix = string.Join(",", ft.Parameters.Select(x => x.DataType.LLVMName()));

                            LLVMValueRef llvmFn;
                            if (root.Name == "GetMember")
                            {
                                var interfGetMember = (ExprNode)root;

                                var interfNode = interfGetMember.Nodes[0];
                                var it = (InterfaceType)interfNode.Type;

                                var vtableNdx = _getVTableNdx(it, ((IdentifierNode)interfGetMember.Nodes[1]).IdName);

                                // move the vtable ndx to the start of the generic in the generic group that the function is
                                // stored in by totalling all the generics vtable offsets (the number of generates that had)
                                // and add that to the starting vtable index
                                vtableNdx += gg.GenericFunctions
                                    .Select(x => (GenericType)x.DataType)
                                    .TakeWhile(x => !x.Generates.Select(y => y.Type).Contains(ft))
                                    .Aggregate(0, (a, b) => a + b.Generates.Count);

                                // then add the index of the desired generate of the appropriate generic function to this to
                                // create the final vtable index
                                vtableNdx += gg.GenericFunctions
                                    .Select((x, i) => new { Type = x, Ndx = i })
                                    .Where(x => x.Type.Generates.Select(y => y.Type).Contains(ft))
                                    .First().Ndx;

                                llvmFn = _generateVtableGet(_generateExpr(interfNode), vtableNdx);
                            }
                            else if (root.Name == "GetTIMethod")
                                llvmFn = _loadGlobalValue(_getLookupName(((ExprNode)root).Nodes[0].Type) + ".variant." + overloadSuffix);
                            else
                                llvmFn = _loadGlobalValue(gg.Name + ".variant." + overloadSuffix);

                            return _generateCall(enode, ft, llvmFn);
                        }
                    case "CallConstructor":
                        return _generateCallConstructor(enode);
                    case "CallTCConstructor":
                        return _generateCallTCConstructor(enode);
                    case "InitList":
                        {
                            var st = (StructType)enode.Nodes[0].Type;
                            var root = _alloca(st, "initl_struct_tmp");

                            foreach (var item in enode.Nodes.Skip(1))
                            {
                                var initializer = (ExprNode)item;

                                var name = ((IdentifierNode)initializer.Nodes[0]).IdName;
                                var initExprNode = initializer.Nodes[1];
                                var initExpr = _generateExpr(initExprNode);

                                var memberType = st.Members[name].DataType;
                                if (!memberType.Equals(initExprNode.Type))
                                    initExpr = _cast(initExpr, initExprNode.Type, memberType);

                                int memberNdx = st.Members.TakeWhile(x => x.Key != name).Count();
                                _setLLVMStructMember(root, initExpr, memberNdx, memberType, $"initl_member.{name}");
                            }

                            return root;
                        }
                    case "From":
                        return _generateFromExpr(enode);
                    case "TypeCast":
                        if (mutableExpr && !_isReferenceType(enode.Type))
                            return _cast(_generateExpr(enode.Nodes[0], mutableExpr), enode.Nodes[0].Type, new PointerType(enode.Type, false));

                        return _cast(_generateExpr(enode.Nodes[0], mutableExpr), enode.Nodes[0].Type, enode.Type);
                    case "ThisDereference":
                        {
                            var root = enode.Nodes[0];
                            var rootExpr = _generateExpr(root);

                            if (_isReferenceType(root.Type))
                                return rootExpr;
                            else
                                return LLVM.BuildLoad(_builder, rootExpr, "this_deref_tmp");
                        }
                    case "Super":
                        return _getSuperForm((InterfaceType)enode.Nodes[0].Type);
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
                            else if (expr.Type is PointerType)
                            {
                                var basePtr = _generateExpr(enode.Nodes[0]);
                                var offset = _generateExpr(enode.Nodes[1]);

                                if (enode.Nodes.Count > 2)
                                {
                                    foreach (var item in enode.Nodes)
                                        offset = LLVM.BuildAdd(_builder, offset, _generateExpr(item), "ptr_add_rop_tmp");
                                }

                                return LLVM.BuildGEP(_builder, basePtr, new[]
                                {
                                    offset
                                }, "ptr_add_tmp");
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
                        {
                            if (expr.Type is PointerType)
                            {
                                var ptInt = LLVM.BuildPtrToInt(_builder, _generateExpr(enode.Nodes[0]), LLVM.Int64Type(), "ptr_int_tmp");

                                foreach (var item in enode.Nodes.Skip(1))
                                {
                                    var baseElem = _generateExpr(item);

                                    if (((SimpleType)item).Type != SimpleType.SimpleClassifier.LONG)
                                        baseElem = _cast(baseElem, item.Type, new SimpleType(SimpleType.SimpleClassifier.LONG));

                                    ptInt = LLVM.BuildSub(_builder, ptInt, baseElem, "ptr_sub_tmp");
                                }

                                return LLVM.BuildIntToPtr(_builder, ptInt, _convertType(expr.Type), "ptr_sub_result_tmp");
                            }
                            else
                            {
                                return _buildNumericBinop(category =>
                                {
                                    if (category == 2)
                                        return LLVM.BuildFSub;
                                    else
                                        return LLVM.BuildSub;
                                }, enode);
                            }
                        }
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
                        {
                            // TODO: check for overloads

                            var siCommonType = (SimpleType)_getCommonType(enode);

                            if (_getSimpleClass(siCommonType) == 0)
                            {
                                if (siCommonType.Type == SimpleType.SimpleClassifier.LONG)
                                    siCommonType = new SimpleType(SimpleType.SimpleClassifier.DOUBLE);
                                else
                                    siCommonType = new SimpleType(SimpleType.SimpleClassifier.FLOAT);
                            }

                            return _buildBinop(LLVM.BuildFDiv, enode, siCommonType, true);
                        }
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
                    case "BAnd":
                        return _buildBinop(LLVM.BuildAnd, enode, _getCommonType(enode));
                    case "BOr":
                        return _buildBinop(LLVM.BuildOr, enode, _getCommonType(enode));
                    case "BXor":
                        return _buildBinop(LLVM.BuildXor, enode, _getCommonType(enode));
                    case "And":
                    case "Or":
                        return _buildShortCircuitBinop(enode, expr.Name == "And");
                    // TODO: short circuit evaluation for logical and and logical or
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
                            // node layout: then, if, else
                            var cond = _generateExpr(enode.Nodes[1]);

                            var thenBlock = LLVM.AppendBasicBlock(_currFunctionRef, "inline_then");
                            var elseBlock = LLVM.AppendBasicBlock(_currFunctionRef, "inline_else");
                            var endBlock = LLVM.AppendBasicBlock(_currFunctionRef, "inline_end");
                            
                            LLVM.PositionBuilderAtEnd(_builder, thenBlock);
                            var thenResult = _generateExpr(enode.Nodes[0]);
                            if (enode.Type.Equals(enode.Nodes[2].Type) && !enode.Type.Equals(enode.Nodes[0]))
                                thenResult = _cast(thenResult, enode.Nodes[0].Type, enode.Nodes[2].Type);
                            LLVM.BuildBr(_builder, endBlock);

                            LLVM.PositionBuilderAtEnd(_builder, elseBlock);
                            var elseResult = _generateExpr(enode.Nodes[2]);
                            if (enode.Type.Equals(enode.Nodes[0].Type) && !enode.Type.Equals(enode.Nodes[2]))
                                elseResult = _cast(elseResult, enode.Nodes[2].Type, enode.Nodes[0].Type);
                            LLVM.BuildBr(_builder, endBlock);

                            LLVM.PositionBuilderAtEnd(_builder, endBlock);
                            var phiNode = LLVM.BuildPhi(_builder, _convertType(enode.Type), "inline_cond_result");
                            phiNode.AddIncoming(new[] { thenResult, elseResult }, new[] { thenBlock, elseBlock }, 2);

                            return phiNode;
                        }
                    case "Then":
                        {
                            var valRes = _generateExpr(enode.Nodes[0]);
                            _scopes.Last()["$VALUE"] = new GeneratorSymbol(valRes);

                            var thenRes = _generateExpr(enode.Nodes[1]);

                            if (!_exprNeedsScope)
                            {
                                _scopes.RemoveLast();
                                _exprNeedsScope = true;
                            }

                            return thenRes;
                        }
                    case "ExprVarDecl":
                        if (_exprNeedsScope)
                        {
                            _exprNeedsScope = false;
                            _scopes.Add(new Dictionary<string, GeneratorSymbol>());
                        }

                        _setVar(((IdentifierNode)enode.Nodes[0]).IdName, _generateExpr(enode.Nodes[1]));
                        break;
                    case "SelectExpr":
                        return _generateSelectExpr(enode);                       
                    case "Indirect":
                        {
                            // nullify indirect if it is just referencing a variable (just use varRef)
                            if (enode.Nodes[0] is IdentifierNode idNode)
                            {
                                var genSym = _getNamedValue(idNode.IdName);

                                if (genSym.IsPointer)
                                    return genSym.Vref;
                            }

                            var ptr = _alloca(enode.Nodes[0].Type, "indirect_tmp");
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
                            var root = _generateExpr(enode.Nodes[0]);

                            LLVMValueRef incRes;
                            if (expr.Type is PointerType)
                                incRes = LLVM.BuildGEP(_builder, root, new[] {
                                    _getI64Zero(), _getOne(new SimpleType(SimpleType.SimpleClassifier.LONG))
                                }, "ptr_increm_tmp");
                            else
                                incRes = LLVM.BuildAdd(_builder, root,
                                _getOne(enode.Type),
                                "increm_tmp");

                            LLVM.BuildStore(_builder, incRes, _generateExpr(enode.Nodes[0], true));

                            return incRes;
                        }
                    case "Decrement":
                        {
                            var root = _generateExpr(enode.Nodes[0]);

                            LLVMValueRef decRes;
                            if (expr.Type is PointerType)
                            {
                                root = LLVM.BuildPtrToInt(_builder, root, LLVM.Int64Type(), "decrem_rootptr_tmp");

                                decRes = LLVM.BuildSub(_builder, root,
                                    LLVM.ConstInt(LLVM.Int64Type(), 1, new LLVMBool(0)),
                                    "decrem_tmp");

                                decRes = LLVM.BuildIntToPtr(_builder, decRes, _convertType(expr.Type), "decrem_ptr_result_tmp");
                            }
                            else
                                decRes = LLVM.BuildSub(_builder, root,
                                    _getOne(enode.Type),
                                    "decrem_tmp");

                            LLVM.BuildStore(_builder, decRes, _generateExpr(enode.Nodes[0], true));

                            return decRes;
                        }
                    case "PostfixIncrement":
                        {
                            var oriVal = _generateExpr(enode.Nodes[0]);

                            LLVMValueRef incRes;
                            if (expr.Type is PointerType)
                                incRes = LLVM.BuildGEP(_builder, oriVal, new[] {
                                    _getI64Zero(), LLVM.ConstInt(LLVM.Int64Type(), 1, new LLVMBool(0))
                                }, "ptr_p_increm_tmp");
                            else
                                incRes = LLVM.BuildAdd(_builder, oriVal,
                                _getOne(enode.Type),
                                "p_increm_tmp");

                            LLVM.BuildStore(_builder, incRes, _generateExpr(enode.Nodes[0], true));

                            return oriVal;
                        }
                    case "PostfixDecrement":
                        {
                            var oriVal = _generateExpr(enode.Nodes[0]);

                            LLVMValueRef decRes;
                            if (expr.Type is PointerType)
                            {
                                var ptrInt = LLVM.BuildPtrToInt(_builder, oriVal, LLVM.Int64Type(), "p_decrem_rootptr_tmp");

                                decRes = LLVM.BuildSub(_builder,
                                    ptrInt,
                                    LLVM.ConstInt(LLVM.Int64Type(), 1, new LLVMBool(0)),
                                    "decrem_tmp");

                                decRes = LLVM.BuildIntToPtr(_builder, decRes, _convertType(expr.Type), "p_decrem_ptr_result_tmp");
                            }
                            else
                                decRes = LLVM.BuildSub(_builder,
                                    oriVal,
                                    _getOne(enode.Type),
                                    "p_decrem_tmp");

                            LLVM.BuildStore(_builder, decRes, _generateExpr(enode.Nodes[0], true));

                            return oriVal;
                        }
                    case "Is":
                        _generateIs(enode);
                        break;
                    // handle initializer expressions (passed from var-decl generator)
                    case "Initializer":
                        return _generateExpr(enode.Nodes[0]);
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

            if (commonType is PointerType)
                instrCat = 0;
            // we assume the common type is a simple type if it is being interpreted as numeric
            else if (((SimpleType)commonType).Unsigned)
                instrCat = 1;
            else if (new[] { SimpleType.SimpleClassifier.FLOAT, SimpleType.SimpleClassifier.DOUBLE}
                .Contains(((SimpleType)commonType).Type))
            {
                instrCat = 2;
            }

            var binopBuilder = nbbfactory(instrCat);

            return _buildBinop(binopBuilder, node, commonType, true);
        }

        private LLVMValueRef _buildShortCircuitBinop(ExprNode enode, bool isAnd)
        {
            var commonType = _getCommonType(enode);

            if (commonType is SimpleType st && st.Type == SimpleType.SimpleClassifier.BOOL)
            {
                var thenBlocks = new List<LLVMBasicBlockRef>();
                var exitBlock = LLVM.AppendBasicBlock(_currFunctionRef, "sc_logical_end");

                var currBlock = LLVM.GetInsertBlock(_builder);
                var finalValue = _ignoreValueRef();
                for (int i = 0; i < enode.Nodes.Count; i++)
                {
                    bool last = i == enode.Nodes.Count - 1;
                    var node = enode.Nodes[i];

                    var operand = _generateExpr(node);
                    thenBlocks.Add(currBlock);

                    if (last)
                    {
                        finalValue = operand;
                        LLVM.BuildBr(_builder, exitBlock);
                        LLVM.PositionBuilderAtEnd(_builder, exitBlock);
                    }
                    else
                    {
                        var thenBlock = LLVM.AppendBasicBlock(_currFunctionRef, "sc_logical_then");

                        if (isAnd)
                            LLVM.BuildCondBr(_builder, operand, thenBlock, exitBlock);
                        else
                            LLVM.BuildCondBr(_builder, operand, exitBlock, thenBlock);

                        currBlock = thenBlock;
                        LLVM.PositionBuilderAtEnd(_builder, currBlock);
                    }                             
                }

                // if the incoming block is not the final block then we know a short circuit occurred
                // therefore we can simply use a constant value as opposed to using the preexisting value
                // not sure if there is a speed difference, but it is probably easier on the codegen
                // select that value based on the operation (false if `and` since false exits `and` and
                // same logic in reverse for `or`)
                var constResult = LLVM.ConstInt(LLVM.Int1Type(), isAnd ? 0u : 1u, new LLVMBool(0));

                var phiNode = LLVM.BuildPhi(_builder, LLVM.Int1Type(), "logical_op_result");

                phiNode.AddIncoming(Enumerable.Repeat(constResult, thenBlocks.Count - 1)
                    .Append(finalValue).ToArray(), thenBlocks.ToArray(), (uint)thenBlocks.Count);

                LLVM.MoveBasicBlockAfter(exitBlock, thenBlocks.Last());

                return phiNode;
            }
            else
                return _buildBinop(isAnd ? (BinopBuilder)LLVM.BuildAnd : LLVM.BuildOr, enode, commonType);
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

        // ---------------------
        // EXPRESSION GENERATORS
        // ---------------------

        // Note: generates as many overloads as it can of a single operator; returns depth it reached
        private int _generateOperatorOverload(ExprNode expr, int startDepth, out LLVMValueRef res)
        {
            if (expr.Nodes.Count - startDepth == 1)
            {
                var opType = expr.Nodes[startDepth].Type;

                if (HasOverload(opType, expr.Name, out DataType _))
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

        private LLVMValueRef _generateTupleLiteral(List<ITypeNode> nodes)
        {
            var tupleType = _createLLVMStructType(nodes.Select(x => x.Type));
            var tupleAlloca = LLVM.BuildAlloca(_builder, tupleType, "tuple_lit_tmp");
            LLVM.SetAlignment(tupleAlloca, nodes
                .Select(x => x.Type)
                .Max(x => _alignOf(x))
                );

            for (int i = 0; i < nodes.Count; i++)
            {
                var tupleElemPtr = LLVM.BuildStructGEP(_builder, tupleAlloca, (uint)i, "tuple_elem_ptr_tmp");
                LLVM.BuildStore(_builder, _generateExpr(nodes[i]), tupleElemPtr);
            }

            return tupleAlloca;
        }

        private LLVMValueRef _generateFromExpr(ExprNode enode)
        {
            var rootType = (CustomNewType)enode.Nodes[0].Type;

            var fromValElemPtr = LLVM.BuildStructGEP(
                _builder,
                _generateExpr(enode.Nodes[0]),
                1,
                "from_elem_ptr_tmp");

            var fromVal = LLVM.BuildLoad(_builder, fromValElemPtr, "from_val_tmp");
            var rootValues = _getTypeClassValues(fromVal, rootType);

            if (rootType.Values.Count > 1)
            {
                var tuple = LLVM.BuildAlloca(_builder, _createLLVMStructType(rootType.Values), "from_tuple_tmp");
                LLVM.SetAlignment(tuple, _alignOf(enode.Type));

                var tupleTypes = ((TupleType)enode.Type).Types;

                for (int i = 0; i < rootType.Values.Count; i++)
                {
                    var tupleElemPtr = LLVM.BuildStructGEP(_builder, tuple, (uint)i, "from_tuple_elem_ptr_tmp");
                    if (_isReferenceType(tupleTypes[i]))
                        _copyLLVMStructTo(tupleElemPtr, rootValues[i]);
                    else
                        LLVM.BuildStore(_builder, rootValues[i], tupleElemPtr);
                }

                return tuple;
            }
            else
                return rootValues[0];
        }
        
        // TODO: handle indefinites
        private LLVMValueRef _generateCall(ExprNode enode, FunctionType ft, LLVMValueRef genFn)
        {
            var argArray = _buildArgArray(ft, enode);

            LLVMValueRef rtPtr = _ignoreValueRef();
            bool returnCallResult = !(ft.ReturnType is NoneType);

            if (_isReferenceType(ft.ReturnType))
            {
                var transformedArgArray = new LLVMValueRef[argArray.Length + 1];

                rtPtr = _alloca(ft.ReturnType, "rtptr_tmp");

                transformedArgArray[0] = rtPtr;
                argArray.CopyTo(transformedArgArray, 1);

                argArray = transformedArgArray;

                returnCallResult = false;
            }

            if (ft.IsBoxed)
            {
                var fElemPtr = LLVM.BuildStructGEP(_builder, genFn, 0, "f_elem_ptr_tmp");
                var fPtr = LLVM.BuildLoad(_builder, fElemPtr, "f_ptr_tmp");

                var statePtr = LLVM.BuildStructGEP(_builder, genFn, 1, "state_ptr_tmp");

                if (ft.IsMethod)
                    statePtr = LLVM.BuildBitCast(_builder, statePtr, fPtr.TypeOf().GetElementType().GetParamTypes()[0], "this_ptr_tmp");

                var boxedFnArgArray = new LLVMValueRef[argArray.Length + 1];
                boxedFnArgArray[0] = statePtr;

                argArray.CopyTo(boxedFnArgArray, 1);

                if (returnCallResult)
                {
                    var callResult = LLVM.BuildCall(_builder, fPtr, boxedFnArgArray, "call_boxed_tmp");
                    return callResult;
                }
                else
                    LLVM.BuildCall(_builder, fPtr, boxedFnArgArray, "");
            }
            else
            {
                if (returnCallResult)
                {
                    var callResult = LLVM.BuildCall(_builder, genFn, argArray, "call_tmp");
                    return callResult;
                }
                else
                    LLVM.BuildCall(_builder, genFn, argArray, "");
            }

            return rtPtr;
        }

        private LLVMValueRef _generateCallConstructor(ExprNode enode)
        {
            var newStruct = _alloca(enode.Type, "nstruct_tmp");

            var st = (StructType)enode.Nodes[0].Type;
            string lookupName = _getLookupName(enode.Type);

            // init members should always exist
            string initMembersLookup = lookupName + "._$initMembers";
            LLVM.BuildCall(_builder, _globalScope[initMembersLookup].Vref,
                    new[] { newStruct }, "");

            string constructorLookup = lookupName + ".constructor";

            FunctionType constr;
            if (st.HasMultipleConstructors())
            {
                var args = _createArgsList(enode);

                st.GetConstructor(args, out constr);

                constructorLookup += "." + string.Join(",", constr.Parameters.Select(x => x.DataType.LLVMName()));
            }
            // implicit default constructor is init members
            else if (!_globalScope.ContainsKey(constructorLookup))
                return newStruct;
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
                var valPtr = _alloca(cnt.Values[0], "tc_valptr_tmp");

                var initExpr = _generateExpr(enode.Nodes[1]);
                if (_isReferenceType(cnt.Values[0]))
                    _copyLLVMStructTo(valPtr, initExpr);
                else
                    LLVM.BuildStore(_builder, initExpr, valPtr);

                valPtr = LLVM.BuildBitCast(_builder, valPtr, _i8PtrType, "tc_valptr_i8_tmp");

                tcRef = LLVM.BuildAlloca(_builder, LLVM.StructType(
                    new[] { LLVM.Int16Type(), valPtr.TypeOf(), LLVM.Int32Type() }, false), "tc_tmp");
                LLVM.SetAlignment(tcRef, WhirlGlobals.POINTER_SIZE);

                var firstElem = LLVM.BuildBitCast(_builder, tcRef, LLVM.PointerType(LLVM.Int16Type(), 0), "tc_enum_ptr_tmp");
                LLVM.BuildStore(_builder, LLVM.ConstInt(LLVM.Int16Type(), (ulong)cnt.Parent.Instances.IndexOf(cnt), new LLVMBool(0)),
                    firstElem);

                var valPtrElem = LLVM.BuildStructGEP(_builder, tcRef, 1, "tc_val_ptr_tmp");
                LLVM.BuildStore(_builder, valPtr, valPtrElem);

                var sizeElem = LLVM.BuildStructGEP(_builder, tcRef, 2, "tc_size_ptr_tmp");
                LLVM.BuildStore(_builder,
                    LLVM.ConstInt(LLVM.Int32Type(), cnt.Values[0].SizeOf(), new LLVMBool(0)),
                    sizeElem);
            }
            else
            {
                var valArrPtr = LLVM.BuildArrayAlloca(
                    _builder,
                    _i8PtrType,
                    LLVM.ConstInt(LLVM.Int32Type(), (ulong)cnt.Values.Count, new LLVMBool(0)),
                    "tc_valarr_tmp"
                    );
                LLVM.SetAlignment(valArrPtr, WhirlGlobals.POINTER_SIZE);

                for (int i = 0; i < cnt.Values.Count; i++)
                {
                    var valPtr = _alloca(cnt.Values[i], "tc_elem_valptr_tmp");

                    var initExpr = _generateExpr(enode.Nodes[i + 1]);
                    if (_isReferenceType(cnt.Values[i]))
                        _copyLLVMStructTo(valPtr, initExpr);
                    else
                        LLVM.BuildStore(_builder, initExpr, valPtr);

                    var valArrElemPtr = LLVM.BuildGEP(_builder, valArrPtr,
                        new[] {
                            LLVM.ConstInt(LLVM.Int32Type(), (ulong)i, new LLVMBool(0))
                        }, "tc_valarr_elem_tmp");

                    var i8ValPtr = LLVM.BuildBitCast(_builder, valPtr, _i8PtrType, "tc_valarr_elem_val_tmp");

                    LLVM.BuildStore(_builder, i8ValPtr, valArrElemPtr);
                }

                tcRef = LLVM.BuildAlloca(_builder, LLVM.StructType(
                    new[] { LLVM.Int16Type(), valArrPtr.TypeOf(), LLVM.Int32Type() }, false), "tc_tmp");
                LLVM.SetAlignment(tcRef, WhirlGlobals.POINTER_SIZE);

                var firstElem = LLVM.BuildBitCast(_builder, tcRef, LLVM.PointerType(LLVM.Int16Type(), 0), "tc_enum_ptr_tmp");
                LLVM.BuildStore(_builder, firstElem,
                    LLVM.ConstInt(LLVM.Int16Type(), (ulong)cnt.Parent.Instances.IndexOf(cnt), new LLVMBool(0)));

                var valArrPtrElem = LLVM.BuildStructGEP(_builder, tcRef, 1, "tc_valarr_ptr_tmp");
                LLVM.BuildStore(_builder, valArrPtr, valArrPtrElem);

                var sizeElem = LLVM.BuildStructGEP(_builder, tcRef, 2, "tc_size_ptr_tmp");
                LLVM.BuildStore(_builder, 
                    LLVM.ConstInt(LLVM.Int32Type(), cnt.Values.Select(x => x.SizeOf()).Aggregate((a, b) => a + b), new LLVMBool(0)), 
                    sizeElem);
            }

            return tcRef;
        }

        private LLVMValueRef _generateGetMember(ExprNode enode, bool mutableExpr)
        {
            string memberName = ((IdentifierNode)enode.Nodes[1]).IdName;

            LLVMValueRef getStructElem(StructType st)
            {
                int memberNdx = st.Members.Keys.ToList().IndexOf(memberName);

                var memberPtr = LLVM.BuildStructGEP(_builder, _generateExpr(enode.Nodes[0]), (uint)memberNdx, "struct_gep_tmp");

                if (mutableExpr || _isReferenceType(st.Members[memberName].DataType))
                    return memberPtr;
                else
                    return LLVM.BuildLoad(_builder, memberPtr, "struct_member." + memberName);
            }

            if (enode.Nodes[0].Type is StructType pst)
                return getStructElem(pst);
            // this only handles regular interfaces since other cases are handled by other functions
            else if (enode.Nodes[0].Type is InterfaceType it)
                return _generateVtableGet(_generateExpr(enode.Nodes[0]), _getVTableNdx(it, memberName));
            // handle friend get membets
            else
            {
                string implName = enode.Nodes[0].Type.LLVMName();

                if (implName.Contains("["))
                    implName = implName.Split("[")[0];
                else if (implName == "str")
                    implName = "string";

                var impl = _impls[implName];

                if (impl is GenericType gt)
                {
                    var rootType = enode.Nodes[0].Type;

                    // assume generic creation works
                    switch (rootType.Classify())
                    {
                        case TypeClassifier.ARRAY:
                            gt.CreateGeneric(new List<DataType> { ((ArrayType)rootType).ElementType }, out impl);
                            break;
                        case TypeClassifier.LIST:
                            gt.CreateGeneric(new List<DataType> { ((ListType)rootType).ElementType }, out impl);
                            break;
                        case TypeClassifier.DICT:
                            {
                                var dct = (DictType)rootType;

                                gt.CreateGeneric(new List<DataType> { dct.KeyType, dct.ValueType }, out impl);
                            }
                            break;
                    }
                }

                return getStructElem((StructType)impl);
            }
        }

        private LLVMValueRef _generateSelectExpr(ExprNode enode)
        {
            var selectExprNode = enode.Nodes[0];
            var selectType = selectExprNode.Type;

            var caseNodes = enode.Nodes.Skip(1).SkipLast(1);
            var caseBlocks = caseNodes
                .Select(x => LLVM.AppendBasicBlock(_currFunctionRef, "case"))
                .ToList();

            var defaultBlock = LLVM.AppendBasicBlock(_currFunctionRef, "default");
            var selectEnd = LLVM.AppendBasicBlock(_currFunctionRef, "select_end");
            var selectResult = _alloca(enode.Type, "select_result_tmp");

            bool noDefault = _generateSelectLogic(selectType, selectExprNode,
                caseNodes.ToList(), caseBlocks, defaultBlock,
                out List<Dictionary<string, GeneratorSymbol>> caseScopes);
            
            using (var e1 = caseNodes.GetEnumerator())
            {
                for (int i = 0; i < caseBlocks.Count; i++)
                {
                    e1.MoveNext();

                    LLVM.PositionBuilderAtEnd(_builder, caseBlocks[i]);
                    _scopes.Add(caseScopes[i]);

                    var exprResult = _generateExpr(((ExprNode)e1.Current).Nodes.Last());
                    if (_isReferenceType(selectType))
                        _copyLLVMStructTo(selectResult, exprResult);
                    else
                        LLVM.BuildStore(_builder, exprResult, selectResult);

                    LLVM.BuildBr(_builder, selectEnd);

                    _scopes.RemoveLast();
                }
            }

            if (noDefault)
                LLVM.RemoveBasicBlockFromParent(defaultBlock);
            else
            {
                LLVM.PositionBuilderAtEnd(_builder, defaultBlock);
                var exprResult = _generateExpr(((ExprNode)enode.Nodes.Last()).Nodes.Last());
                if (_isReferenceType(selectType))
                    _copyLLVMStructTo(selectResult, exprResult);
                else
                    LLVM.BuildStore(_builder, exprResult, selectResult);

                LLVM.BuildBr(_builder, selectEnd);
            }

            LLVM.MoveBasicBlockAfter(selectEnd, LLVM.GetLastBasicBlock(_currFunctionRef));
            LLVM.PositionBuilderAtEnd(_builder, selectEnd);
            return selectResult;
        }

        private LLVMValueRef _generateIs(ExprNode enode)
        {
            var rootExpr = enode.Nodes[0];
            var checkType = enode.Nodes[1].Type;
            var rootVal = _generateExpr(rootExpr);

            LLVMValueRef rtVal;
            if (rootExpr.Type.Equals(checkType))
                rtVal = LLVM.ConstInt(LLVM.Int1Type(), 1, new LLVMBool(0));
            else
            {
                var cValType = new SimpleType(SimpleType.SimpleClassifier.SHORT);

                switch (rootExpr)
                {
                    case InterfaceType it:
                        {
                            var cVal = _getLLVMStructMember(rootVal, 2, cValType);
                            var otherCVal = LLVM.ConstInt(LLVM.Int16Type(), _getTypeCVal(checkType), new LLVMBool(0));

                            rtVal = LLVM.BuildICmp(_builder, LLVMIntPredicate.LLVMIntEQ, cVal, otherCVal, "is_cmp_tmp");
                        }
                        break;
                    case CustomInstance ci:
                        {
                            if (checkType is CustomInstance cci && ci.Parent.Equals(cci.Parent))
                            {
                                var cVal = _getLLVMStructMember(rootVal, 1, cValType);
                                var otherCVal = LLVM.ConstInt(LLVM.Int16Type(), (ulong)ci.Parent.Instances.IndexOf(cci), new LLVMBool(0));

                                rtVal = LLVM.BuildICmp(_builder, LLVMIntPredicate.LLVMIntEQ, cVal, otherCVal, "is_cmp_tmp");
                            }
                            else
                                rtVal = LLVM.ConstInt(LLVM.Int1Type(), 0, new LLVMBool(0));
                        }
                        break;
                    case AnyType at:
                        {
                            var cVal = _getLLVMStructMember(rootVal, 1, cValType);
                            var otherCVal = LLVM.ConstInt(LLVM.Int16Type(), _getTypeCVal(checkType), new LLVMBool(0));

                            rtVal = LLVM.BuildICmp(_builder, LLVMIntPredicate.LLVMIntEQ, cVal, otherCVal, "is_cmp_tmp");
                        }
                        break;
                    // we can assume that is should fail
                    default:
                        rtVal = LLVM.ConstInt(LLVM.Int1Type(), 0, new LLVMBool(0));
                        break;
                }
            }        

            if (enode.Nodes[1] is IdentifierNode idNode)
            {
                var startBlock = LLVM.GetInsertBlock(_builder);
                var patternSucc = LLVM.AppendBasicBlock(_currFunctionRef, "is_pattern_succ");
                var patternEnd = LLVM.AppendBasicBlock(_currFunctionRef, "is_pattern_end");

                LLVM.BuildCondBr(_builder, rtVal, patternSucc, patternEnd);

                LLVM.PositionBuilderAtEnd(_builder, patternSucc);
                var castRoot = _cast(rootVal, rootExpr.Type, checkType);

                LLVM.PositionBuilderAtEnd(_builder, patternEnd);
                var phiNode = LLVM.BuildPhi(_builder, _convertType(checkType), "pattern_result_tmp");
                phiNode.AddIncoming(new[] { castRoot, _getNullValue(checkType, true) }, new[] { patternSucc, startBlock }, 2);

                var resultVar = _alloca(checkType, "pattern_result_var");
                LLVM.BuildStore(_builder, phiNode, resultVar);
                _setVar(idNode.IdName, resultVar);
            }
            else if (enode.Nodes.Count == 3)
            {
                var isPattern = (ExprNode)enode.Nodes[2];

                var startBlock = LLVM.GetInsertBlock(_builder);
                var patternSucc = LLVM.AppendBasicBlock(_currFunctionRef, "is_extract_pattern_succ");
                var patternEnd = LLVM.AppendBasicBlock(_currFunctionRef, "is_extract_pattern_end");

                // allocate output variables
                List<LLVMValueRef> outputVars = isPattern.Nodes
                    .Where(x => x.Name != "_")
                    .Select(x => _alloca(x.Type))
                    .ToList();

                LLVM.BuildCondBr(_builder, rtVal, patternSucc, patternEnd);

                LLVM.PositionBuilderAtEnd(_builder, patternSucc);
                var tcElems = _getTypeClassValues(rootVal, ((CustomInstance)checkType));
                using (var outputVarE = outputVars.GetEnumerator())
                {
                    outputVarE.MoveNext();

                    for (int i = 0; i < isPattern.Nodes.Count; i++)
                    {
                        var currNode = isPattern.Nodes[i];
                        var tcElem = tcElems[i];

                        if (currNode.Name == "_")
                        {
                            LLVM.InstructionRemoveFromParent(tcElem);
                            continue;
                        }

                        outputVarE.MoveNext();
                        var outputVar = outputVarE.Current;

                        if (_isReferenceType(currNode.Type))
                            _copyLLVMStructTo(outputVar, tcElem);
                        else
                            LLVM.BuildStore(_builder, tcElem, outputVar);

                        _setVar(((IdentifierNode)isPattern.Nodes[i]).Name, outputVar, true);
                    }
                }
                    

                LLVM.PositionBuilderAtEnd(_builder, patternEnd);
            }

            return rtVal;
        }

        // -----------------------
        // EXPR GENERATION HELPERS
        // -----------------------

        // TODO: handle indefinites
        private LLVMValueRef[] _buildArgArray(FunctionType ft, ExprNode enode)
        {
            var argArray = new LLVMValueRef[ft.Parameters.Count];
            var argArrayTypes = new DataType[ft.Parameters.Count];

            if (argArray.Length == 0)
                return argArray;

            int i = 1;
            for (; i < enode.Nodes.Count; i++)
            {
                var item = enode.Nodes[i];

                if (item.Name == "NamedArgument")
                    break;

                argArray[i - 1] = _copy(_generateExpr(item), item.Type);
                argArrayTypes[i - 1] = item.Type;
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
                    argArrayTypes[ftNdx] = item.Nodes[1].Type;
                }
            }

            for (int k = 0; k < argArray.Length; k++)
            {
                if (!ft.Parameters[k].DataType.Equals(argArrayTypes[k]))
                    argArray[k] = _cast(argArray[k], argArrayTypes[k], ft.Parameters[k].DataType);  
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

        private LLVMValueRef _getI64Zero()
        {
            return LLVM.ConstInt(LLVM.Int64Type(), 0, new LLVMBool(0));
        }

        private LLVMValueRef _boxFunction(LLVMValueRef fn, LLVMValueRef state)
        {
            var i8StatePtr = LLVM.BuildBitCast(_builder, state, _i8PtrType, "state_ptr_tmp");

            var boxedFunctionStruct = LLVM.BuildAlloca(_builder, LLVM.StructType(new[] { fn.TypeOf(), _i8PtrType }, false), "boxed_fn_tmp");
            LLVM.SetAlignment(boxedFunctionStruct, WhirlGlobals.POINTER_SIZE);

            var fnElem = LLVM.BuildStructGEP(_builder, boxedFunctionStruct, 0, "boxed_fn_fnptr_tmp");
            LLVM.BuildStore(_builder, fn, fnElem);

            var stateElem = LLVM.BuildStructGEP(_builder, boxedFunctionStruct, 1, "boxed_fn_stateptr_tmp");
            LLVM.BuildStore(_builder, state, stateElem);

            return boxedFunctionStruct;
        }

        private LLVMValueRef _getSuperForm(InterfaceType superInterf)
        {
            var thisVref = _getNamedValue("this").Vref;

            // we know `this` is always a pointer so just do a fast bitcast
            var i8ThisPtr = LLVM.BuildBitCast(_builder, thisVref, _i8PtrType, "i8_this_ptr_tmp");

            var interfStruct = _alloca(superInterf, "super_interf_tmp");

            _setLLVMStructMember(interfStruct, i8ThisPtr, 0, new PointerType(new SimpleType(SimpleType.SimpleClassifier.BYTE, true), false));

            var dt = new NoneType(); // TEMPORARY

            // SUPER VTABLE

            var cVal = LLVM.ConstInt(LLVM.Int16Type(), _getTypeCVal(dt), new LLVMBool(0));
            _setLLVMStructMember(interfStruct, cVal, 2, new SimpleType(SimpleType.SimpleClassifier.SHORT, true));

            var size = LLVM.ConstInt(LLVM.Int32Type(), dt.SizeOf(), new LLVMBool(0));
            _setLLVMStructMember(interfStruct, size, 3, new SimpleType(SimpleType.SimpleClassifier.INTEGER, true));

            return interfStruct;
        }
    }
}
