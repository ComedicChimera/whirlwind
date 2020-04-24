using System.Collections.Generic;
using System.Linq;
using System;

using LLVMSharp;

using Whirlwind.Semantic;
using Whirlwind.Types;

namespace Whirlwind.Generation
{
    partial class Generator
    {
        // TODO: external type classes
        private void _generateTypeClass(BlockNode node)
        {
            var tc = (CustomType)node.Nodes[0].Type;
            int buildType = 0;

            if (tc.Instances.Count == 1)
            {
                if (tc.Instances.First() is CustomNewType cnt)
                {
                    if (cnt.Values.Count == 1)
                        buildType = 1;
                    else if (cnt.Values.Count > 1)
                        buildType = 2;
                }
            }
            else
            {
                IEnumerable<int> cntFields = tc.Instances
                    .Where(x => x is CustomNewType)
                    .Select(x => ((CustomNewType)x).Values.Count);

                // if there are not new types, then we have a type union => type 1
                if (cntFields.Count() == 0 || cntFields.Max() == 1)
                    buildType = 1;
                else if (cntFields.Max() > 1)
                    buildType = 2;
            }

            if (buildType > 0)
            {
                string name = ((IdentifierNode)node.Nodes[0]).IdName;

                var symbol = _symTable[name];
                name += _genericSuffix;

                bool exported = symbol.Modifiers.Contains(Modifier.EXPORTED);

                var tcStruct = LLVM.StructCreateNamed(_ctx, exported ? _randPrefix + name : name);

                var valueHolder = _i8PtrType;
                if (buildType == 2)
                    valueHolder = LLVM.PointerType(valueHolder, 0);

                tcStruct.StructSetBody(new[]
                {
                    LLVM.Int16Type(),
                    valueHolder,
                    LLVM.Int32Type()
                }, true);

                _globalStructs[name] = tcStruct;
            }
        }

        private List<LLVMValueRef> _getTypeClassValues(LLVMValueRef tc, CustomInstance tcdt)
        {
            if (tcdt.Parent.IsReferenceType())
            {
                if (tcdt is CustomAlias ca)
                {
                    var tcAliasVal = LLVM.BuildStructGEP(_builder, tc, 0, "alias_elem_ptr_tmp");
                    tcAliasVal = LLVM.BuildLoad(_builder, tcAliasVal, "alias_i8ptr_tmp");

                    tcAliasVal = LLVM.BuildBitCast(_builder, tcAliasVal, LLVM.PointerType(_convertType(ca.Type), 0), "alias_ptr_tmp");

                    if (!_isReferenceType(ca.Type))
                        tcAliasVal = LLVM.BuildLoad(_builder, tcAliasVal, "alias_tmp");

                    return new List<LLVMValueRef> { tcAliasVal };
                }
                else if (tcdt is CustomNewType cnt)
                {
                    if (cnt.Values.Count == 1)
                    {
                        var tcAlgVal = LLVM.BuildStructGEP(_builder, tc, 0, "algval_elem_ptr_tmp");
                        tcAlgVal = LLVM.BuildLoad(_builder, tcAlgVal, "algval_i8ptr_tmp");

                        var algType = cnt.Values[0];

                        tcAlgVal = LLVM.BuildBitCast(_builder, tcAlgVal,
                            LLVM.PointerType(_convertType(algType), 0), "algval_ptr_tmp");

                        if (!_isReferenceType(algType))
                            tcAlgVal = LLVM.BuildLoad(_builder, tcAlgVal, "algval_tmp");

                        return new List<LLVMValueRef> { tcAlgVal };
                    }
                    // assume cnt.Values > 1
                    else
                    {
                        var valList = new List<LLVMValueRef>();

                        for (int i = 0; i < cnt.Values.Count; i++)
                        {
                            var tcAlgValArrElemPtr = LLVM.BuildInBoundsGEP(_builder, tc,
                                new[]
                                {
                            LLVM.ConstInt(LLVM.Int32Type(), (ulong)i, new LLVMBool(0)),
                            LLVM.ConstInt(LLVM.Int32Type(), 0, new LLVMBool(0))
                                }, "alg_valarr_elem_ptr_tmp");

                            var tcAlgValArrElem = LLVM.BuildBitCast(
                                _builder,
                                tcAlgValArrElemPtr,
                                LLVM.PointerType(_convertType(cnt.Values[i]), 0),
                                "alg_valarr_elem_cast_tmp"
                                );

                            if (!_isReferenceType(cnt.Values[i]))
                                tcAlgValArrElem = LLVM.BuildLoad(_builder, tcAlgValArrElem, "alg_valarr_elem_tmp");

                            valList.Add(tcAlgValArrElem);
                        }

                        return valList;
                    }
                }
                else
                    throw new InvalidOperationException("Cannot extract value from an opaque type class");
            }
            else
                return new List<LLVMValueRef> { tc };
        }
    }
}
