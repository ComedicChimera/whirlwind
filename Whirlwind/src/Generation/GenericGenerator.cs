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
        private delegate void GenericGenerator(BlockNode node, bool external);

        private void _generateGeneric(BlockNode node)
        {
            var genericType = (GenericType)((IdentifierNode)node.Nodes[0]).Type;
            GenericGenerator gg;

            switch (genericType.DataType.Classify())
            {
                case TypeClassifier.FUNCTION:
                    gg = (n, e) => _generateFunction(n, e, true);
                    break;
                case TypeClassifier.TYPE_CLASS_INSTANCE:
                    gg = (n, _) => _generateTypeClass(n);
                    break;
                case TypeClassifier.INTERFACE_INSTANCE:
                    gg = (n, e) => _generateInterf(n);
                    break;
                default:
                    gg = (n, e) => _generateStruct(n, e);
                    break;
            }

            foreach (var generate in genericType.Generates)
            {
                _genericSuffix = ".variant." + string.Join("_", generate.GenericAliases
                    .Values.Select(x => x.LLVMName()));

                gg(generate.Block, false);
            }

            _genericSuffix = "";
        }

        private string _pushGenericSuffix(string newSuffix)
        {
            string oldSuffix = _genericSuffix;
            _genericSuffix = newSuffix;

            return oldSuffix;
        }

        private List<Tuple<string, FunctionType, BlockNode>> _generateGenericMethod(string interfName, Symbol symbol)
        {
            string llvmNameBase;
            if (_getOperatorOverloadName(symbol.Name, out string overloadName))
                llvmNameBase = interfName + ".operator." + overloadName;
            else
                llvmNameBase = interfName + ".interf." + symbol.Name;

            var generateMethods = new List<Tuple<string, FunctionType, BlockNode>>();
            foreach (var generate in ((GenericType)symbol.DataType).Generates)
            {
                string llvmName = llvmNameBase + ".variant." + string.Join("_", generate.GenericAliases
                    .Values.Select(x => x.LLVMName()));

                generateMethods.Add(new Tuple<string, FunctionType, BlockNode>(
                    llvmName, (FunctionType)generate.Type, generate.Block
                    ));
            }

            return generateMethods;
        }

        private LLVMValueRef _generateCreateGeneric(ExprNode enode)
        {
            var root = enode.Nodes[0];

            // only time this is valid (pure GetMember) is with interfaces
            if (root.Name == "GetMember")
            {
                var interfGetMember = (ExprNode)root;

                var itType = (InterfaceType)interfGetMember.Nodes[0].Type;
                string name = ((IdentifierNode)interfGetMember.Nodes[1]).IdName;

                int vtableNdx = _getVTableNdx(itType, name);
                itType.GetFunction(name, out Symbol sym);

                vtableNdx += ((GenericType)sym.DataType).Generates
                    .Select((x, i) => new { x.Type, Ndx = i })
                    .Where(x => x.Type == enode.Type).First().Ndx;

                return _generateVtableGet(_generateExpr(interfGetMember.Nodes[0]), vtableNdx);
            }

            var generate = ((GenericType)root.Type).Generates.Single(x => enode.Type.GenerateEquals(x.Type));
            string typeListSuffix = string.Join(',', generate.GenericAliases.Select(x => x.Value.LLVMName()));

            if (root.Name == "GetTIMethod")
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

                var tiGenerateName = rootName + ".interf." + memberName + ".variant." + typeListSuffix;

                return _boxFunction(_globalScope[tiGenerateName].Vref, typeInterf);
            }

            // assume root is an Identifier node or package static get
            var generateName = _getIdentifierName(root) + ".variant." + typeListSuffix;

            // structs are handled in the CallConstructor handler
            // and interfaces are never used this way
            return _globalScope[generateName].Vref;
        }
    }
}
