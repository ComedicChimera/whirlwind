﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using LLVMSharp;

using Whirlwind.Semantic;
using Whirlwind.Types;

namespace Whirlwind.Generation
{
    partial class Generator
    {
        private LLVMValueRef _generateExpr(ITypeNode expr)
        {
            if (expr is ValueNode vnode)
                return _generateExprValue(vnode);
            // hopefully this is ok
            else if (expr is IdentifierNode inode)
                return _getNamedValue(inode.IdName);
            else if (expr is ConstexprNode cnode)
                return _generateExpr(cnode.ConstValue);
            // only other option is expr node
            else
            {
                var enode = (ExprNode)expr;

                switch (expr.Name)
                {
                    case "Array":
                        {
                            var elemType = ((ArrayType)expr.Type).ElementType;

                            var llvmElementType = _convertType(elemType);
                            var llvmArrayType = LLVM.ArrayType(llvmElementType, (uint)enode.Nodes.Count);

                            var arrLit = LLVM.BuildAlloca(_builder, llvmArrayType, "array_lit");

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
                }
            }

            return _ignoreValueRef();
        }

        private List<LLVMValueRef> _buildOperands(List<ITypeNode> nodes, DataType exprType)
        {
            var results = new List<LLVMValueRef>();

            foreach (var node in nodes)
            {
                var g = _generateExpr(node);

                if (!exprType.Equals(node.Type))
                    results.Add(_cast(g, node.Type, exprType));
                else
                    results.Add(g);
            }

            return results;
        }

        private bool _buildOperOverload(ITypeNode expr, out LLVMValueRef res)
        {
            res = _ignoreValueRef();
            return false;
        } 
    }
}
