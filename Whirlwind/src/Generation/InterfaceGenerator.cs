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

            var methods = _generateInterfBody(node, interfType, name, false, exported);

            var vtableStruct = LLVM.StructCreateNamed(_ctx, llvmPrefix + name + ".__vtable");
            vtableStruct.StructSetBody(methods.ToArray(), false);

            _globalStructs[name + ".__vtable"] = vtableStruct;

            var interfStruct = LLVM.StructCreateNamed(_ctx, llvmPrefix + name);
            interfStruct.StructSetBody(new[]
            {
                LLVM.PointerType(LLVM.Int8Type(), 0),
                LLVM.PointerType(vtableStruct, 0),
                LLVM.Int16Type()
            }, false);

            _globalStructs[name] = interfStruct;
        }

        private List<LLVMTypeRef> _generateInterfBody(BlockNode node, InterfaceType interfType, string name, bool typeInterf, bool exported)
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
                        if (typeInterf && method.Value == MethodStatus.VIRTUAL)
                        {
                            var parentInterf = interfType.Implements.Single(x => x.Methods.Contains(method));

                            _addGlobalDecl(llvmName, _globalScope[llvmName.Replace(name, _getLookupName(parentInterf))].Vref);
                        }

                        var llvmMethod = _generateFunctionPrototype(llvmName, fnType, exported);

                        _appendFunctionBlock(llvmMethod, ((BlockNode)node.Block[methodNdx]));

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
                            if (typeInterf && method.Value == MethodStatus.VIRTUAL)
                            {
                                var parentInterf = interfType.Implements.Single(x => x.Methods.Contains(method));

                                _addGlobalDecl(generate.Item1, _globalScope[generate.Item1.Replace(name, _getLookupName(parentInterf))].Vref);
                            }

                            var llvmMethod = _generateFunctionPrototype(generate.Item1, generate.Item2, exported);

                            _appendFunctionBlock(llvmMethod, generate.Item3);

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

        // TODO: exported interface bindings
        private void _generateInterfBind(BlockNode node)
        {
            var it = (InterfaceType)node.Nodes[0].Type;
            var dtBindName = _getLookupName(node.Nodes[1].Type);

            _generateInterfBody(node, it, dtBindName, true, false);
        }

        private void _generateGenericBind(BlockNode node)
        {
            var gt = (GenericType)node.Nodes[0].Type;
            
            foreach (var item in gt.Generates)
            {
                var dtBindName = _getLookupName(item.Type);

                _generateInterfBody(item.Block, (InterfaceType)item.Type, dtBindName, true, false);
            }
        }
    }
}
