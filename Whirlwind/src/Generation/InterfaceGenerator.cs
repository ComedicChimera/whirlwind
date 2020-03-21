using System;
using System.Collections.Generic;
using System.Linq;

using LLVMSharp;

using Whirlwind.Semantic;
using Whirlwind.Types;

namespace Whirlwind.Generation
{
    partial class Generator
    {
        // TODO: external interfaces
        private void _generateInterf(BlockNode node)
        {
            var idNode = (IdentifierNode)node.Nodes[0];
            var interfType = (InterfaceType)idNode.Type;

            string name = idNode.IdName;

            _table.Lookup(idNode.IdName, out Symbol interfSymbol);
            bool exported = interfSymbol.Modifiers.Contains(Modifier.EXPORTED);

            string llvmPrefix = exported ? _randPrefix : "";
            name += _genericSuffix;

            var interfStruct = LLVM.StructCreateNamed(_ctx, llvmPrefix + name);

            _thisPtrType = LLVM.PointerType(interfStruct, 0);
            var methods = _generateInterfBody(node, interfType, _thisPtrType, name, false, exported);

            var vtableStruct = LLVM.StructCreateNamed(_ctx, llvmPrefix + name + ".__vtable");
            vtableStruct.StructSetBody(methods.ToArray(), false);

            _globalStructs[name + ".__vtable"] = vtableStruct;
      
            interfStruct.StructSetBody(new[]
            {
                _i8PtrType,
                LLVM.PointerType(vtableStruct, 0),
                LLVM.Int16Type(),
                LLVM.Int32Type()
            }, false);

            _globalStructs[name] = interfStruct;

            _thisPtrType = LLVM.PointerType(_interfBoxType, 0);
        }

        private List<LLVMTypeRef> _generateInterfBody(BlockNode node, InterfaceType interfType, LLVMTypeRef desiredThis, 
            string name, bool typeInterf, bool exported)
        {
            var methods = new List<LLVMTypeRef>();

            int methodNdx = 0;
            foreach (var method in interfType.Methods)
            {
                if (method.Key.DataType is FunctionType fnType)
                {
                    string llvmName;
                    if (_getOperatorOverloadName(method.Key.Name, out string overloadName))
                        llvmName = name + ".operator." + overloadName;
                    else
                        llvmName = name + ".interf." + method.Key.Name;

                    if (interfType.Methods.Where(x => x.Key.Name == method.Key.Name).Count() > 1)
                        llvmName += "." + string.Join(",", fnType.Parameters.Select(x => x.DataType.LLVMName()));

                    methods.Add(_convertType(fnType));

                    if (method.Value != MethodStatus.ABSTRACT)
                    {
                        var llvmMethod = _generateFunctionPrototype(llvmName, fnType, exported);

                        if (typeInterf)
                            _appendFunctionBlock(llvmMethod, fnType.ReturnType, _getTIMethodBodyBuilder((BlockNode)node.Block[methodNdx], desiredThis));
                        else
                            _appendFunctionBlock(llvmMethod, fnType.ReturnType, (BlockNode)node.Block[methodNdx]);

                        _addGlobalDecl(llvmName, llvmMethod);
                    }
                }
                // generic case
                else
                {
                    var generateList = _generateGenericMethod(name, method.Key);

                    if (method.Value != MethodStatus.ABSTRACT)
                    {
                        foreach (var generate in generateList)
                        {
                            var llvmMethod = _generateFunctionPrototype(generate.Item1, generate.Item2, exported);

                            if (typeInterf)
                                _appendFunctionBlock(llvmMethod, generate.Item2.ReturnType, _getTIMethodBodyBuilder(generate.Item3, desiredThis));
                            else
                                _appendFunctionBlock(llvmMethod, generate.Item2.ReturnType, generate.Item3);

                            _addGlobalDecl(generate.Item1, llvmMethod);
                        }
                    }
                }
            }

            return methods;
        }

        Dictionary<string, string> _opNameTable = new Dictionary<string, string>
        {
            { "+", "Add" },
            { "-", "Sub" },
            { "*", "Mul" },
            { "/", "Div" },
            { "~/", "Floordiv" },
            { "~^", "Pow" },
            { "%", "Mod" },
            { ">", "Gt" },
            { "<", "Lt" },
            { ">=", "GtEq" },
            { "<=", "LtEq" },
            { "==", "Eq" },
            { "!=", "Neq" },
            { "!", "Not" },
            { "AND", "And" },
            { "OR", "Or" },
            { "XOR", "Xor" },
            { "~", "Complement" },
            { ">>", "RShift" },
            { "<<", "LShift" },
            { "~*", "Compose" },
            { ">>=", "Bind" },
            { "[]", "Subscript" },
            { "[:]", "Slice" },
            { "..", "Range" },
            { ":>", "ExtractInto" }
        };

        private bool _getOperatorOverloadName(string baseName, out string outputName)
        {
            string trimmedName = baseName.Trim('_');

            if (_opNameTable.ContainsKey(trimmedName))
            {
                outputName = _opNameTable[trimmedName];
                return true;
            }

            outputName = "";
            return false;
        }

        private FnBodyBuilder _getTIMethodBodyBuilder(BlockNode block, LLVMTypeRef thisPtrType)
            => delegate (LLVMValueRef method)
            {
                var thisVref = _getNamedValue("this").Vref;

                var thisElemPtr = LLVM.BuildStructGEP(_builder, thisVref, 0, "this_elem_ptr_tmp");
                var i8ThisPtr = LLVM.BuildLoad(_builder, thisElemPtr, "this_i8ptr_tmp");

                _setVar("this", LLVM.BuildBitCast(_builder, i8ThisPtr, thisPtrType, "__this"));

                return _generateBlock(block.Block);
            };

        // TODO: exported interface bindings
        private void _generateInterfBind(BlockNode node)
        {
            var it = (InterfaceType)node.Nodes[0].Type;
            var dtBindName = _getLookupName(node.Nodes[1].Type);
            var bindThisPtrType = _getBindThisPtrType(node.Nodes[1].Type);

            _generateInterfBody(node, it, bindThisPtrType, dtBindName, true, false);
        }

        private void _generateGenericBind(BlockNode node)
        {
            var gt = (GenericType)node.Nodes[0].Type;
            
            foreach (var item in gt.Generates)
            {
                var dtBindName = _getLookupName(item.Type);
                var bindThisPtrType = _getBindThisPtrType(item.Type);

                _generateInterfBody(item.Block, (InterfaceType)item.Type, bindThisPtrType, dtBindName, true, false);
            }
        }

        private LLVMTypeRef _getBindThisPtrType(DataType dt)
        {
            var baseType = _convertType(dt);

            return LLVM.PointerType(baseType, 0);
        }

        private LLVMValueRef _boxToInterf(LLVMValueRef vref, DataType dt)
        {
            var boxed = LLVM.BuildAlloca(_builder, _interfBoxType, "interf_box_tmp");        

            LLVMValueRef thisPtr;
            if (dt.IsThisPtr || _isReferenceType(dt))
                thisPtr = LLVM.BuildBitCast(_builder, vref, _i8PtrType, "this_i8_ptr_tmp");
            else
            {
                thisPtr = LLVM.BuildAlloca(_builder, _convertType(dt), "this_ptr_tmp");
                LLVM.BuildStore(_builder, vref, thisPtr);

                thisPtr = LLVM.BuildBitCast(_builder, thisPtr, _i8PtrType, "this_i8_ptr_tmp");
            }

            var thisElemPtr = LLVM.BuildStructGEP(_builder, boxed, 0, "this_elem_ptr_tmp");
            LLVM.BuildStore(_builder, thisPtr, thisElemPtr);

            // because this is a fake box (not actually intended to be used a real interface, c val and size are not necessary)

            return boxed;
        }
    }
}
