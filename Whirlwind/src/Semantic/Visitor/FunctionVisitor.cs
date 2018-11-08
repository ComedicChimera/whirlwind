using Whirlwind.Types;
using Whirlwind.Parser;

using System.Collections.Generic;
using System.Linq;

namespace Whirlwind.Semantic.Visitor
{
    partial class Visitor
    {
        private void _visitFunction(ASTNode function)
        {
            bool isAsync = false;
            string name = "";
            var parameters = new List<Parameter>();
            var dataTypes = new List<IDataType>();
            var namePosition = new TextPosition();

            foreach (var item in function.Content)
            {
                switch (item.Name)
                {
                    case "TOKEN":
                        {
                            Token tok = ((TokenNode)item).Tok;

                            switch (tok.Type)
                            {
                                case "ASYNC":
                                    isAsync = true;
                                    break;
                                case "IDENTIFIER":
                                    name = tok.Value;
                                    namePosition = item.Position;
                                    break;
                                case ";":
                                    {
                                        _createFunction(parameters, dataTypes, name, namePosition, isAsync);
                                        // try to complete body
                                    }
                                    break;
                            }
                        }
                        break;
                    case "args_decl_list":
                        parameters = _generateArgsDecl((ASTNode)item);
                        break;
                    case "types":
                        dataTypes = _generateTypeList((ASTNode)item);
                        break;
                    case "func_body":
                        {
                            _createFunction(parameters, dataTypes, name, namePosition, isAsync);
                            _visitFuncBody((ASTNode)item);
                        }
                        break;
                }
            }
        }

        private void _createFunction(
            List<Parameter> parameters, List<IDataType> dataTypes, string name, TextPosition namePosition, bool isAsync
            )
        {
            _nodes.Add(new BlockNode(isAsync ? "AsyncFunction" : "Function"));

            IDataType finalType;

            if (dataTypes.Count == 0)
                finalType = new SimpleType();
            else if (dataTypes.Count == 1)
                finalType = dataTypes[0];
            else
                finalType = new TupleType(dataTypes);

            _nodes.Add(new IdentifierNode(name,
                new FunctionType(
                    parameters,
                    finalType,
                    isAsync),
                true));

            MergeBack();
            if (!_table.AddSymbol(new Symbol(name, finalType, new List<Modifier>() { Modifier.CONSTANT })))
                throw new SemanticException($"Unable to redeclare symbol by name {name}", namePosition);
        }

        public List<Parameter> _generateArgsDecl(ASTNode node)
        {
            var argsDeclList = new List<Parameter>(); 
            foreach (var subNode in node.Content)
            {
                if (subNode.Name == "decl_arg")
                {
                    bool optional = false, 
                        constant = false,
                        setParamType = false;
                    var identifiers = new List<string>();
                    IDataType paramType = new SimpleType();

                    foreach (var argPart in ((ASTNode)subNode).Content)
                    {
                        switch (argPart.Name)
                        {
                            case "TOKEN":
                                if (((TokenNode)argPart).Tok.Type == "IDENTIFIER")
                                    identifiers.Add(((TokenNode)argPart).Tok.Value);
                                break;
                            case "arg_ext":
                                foreach (var extensionPart in ((ASTNode)argPart).Content)
                                {
                                    if (extensionPart.Name == "types")
                                    {
                                        paramType = _generateType((ASTNode)extensionPart);
                                        setParamType = true;
                                    }
                                    else if (extensionPart.Name == "TOKEN")
                                    {
                                        if (((TokenNode)extensionPart).Tok.Type == "@")
                                            constant = true;
                                    }
                                }
                                
                                break;
                            case "initializer":
                                _visitExpr((ASTNode)((ASTNode)argPart).Content[1]);
                                optional = true;
                                break;
                        }
                    }

                    if (!optional && !setParamType)
                        throw new SemanticException("Unable to create argument with no type", subNode.Position);

                    if (setParamType && optional && !paramType.Coerce(_nodes.Last().Type))
                        throw new SemanticException("Initializer type incompatable with type extension", subNode.Position);

                    if (optional)
                    {
                        foreach (var identifier in identifiers)
                            argsDeclList.Add(new Parameter(identifier, paramType, false, constant, _nodes.Last()));

                        _nodes.RemoveAt(_nodes.Count - 1); // remove argument from node stack
                    }
                    else
                    {
                        foreach (var identifier in identifiers)
                            argsDeclList.Add(new Parameter(identifier, paramType, false, constant));
                    }
                }
                else if (subNode.Name == "ending_arg")
                {
                    bool constant = false;
                    string name = "";
                    IDataType dt = new SimpleType();

                    foreach (var item in ((ASTNode)subNode).Content)
                    {
                        if (item.Name == "extension")
                            dt = _generateType((ASTNode)((ASTNode)item).Content[1]);
                        else if (item.Name == "TOKEN")
                        {
                            switch (((TokenNode)item).Tok.Type)
                            {
                                case "IDENTIFIER":
                                    name = ((TokenNode)item).Tok.Value;
                                    break;
                                case "@":
                                    constant = true;
                                    break;
                            }
                        }
                    }

                    argsDeclList.Add(new Parameter(name, dt, true, constant));
                }
            }

            if (argsDeclList.GroupBy(x => x.Name).Any(x => x.Count() > 1))
                throw new SemanticException("Function cannot be declared with duplicate arguments", node.Position);

            return argsDeclList;
        }

        private IDataType _visitFuncBody(ASTNode node)
        {
            return new SimpleType();
        }

        // generate a parameter list from a function call and generate the corresponding tree
        private List<ParameterValue> _generateArgsList(ASTNode node)
        {
            var argsList = new List<ParameterValue>();
            foreach (var subNode in node.Content)
            {
                if (subNode.Name == "arg")
                {
                    var arg = (ASTNode)subNode;

                    if (arg.Content[0].Name == "expr")
                    {
                        _visitExpr((ASTNode)arg.Content[0]);
                        argsList.Add(new ParameterValue(_nodes.Last().Type));
                    }
                    else
                    {
                        string name = ((TokenNode)arg.Content[0]).Tok.Value;
                        _visitExpr((ASTNode)((ASTNode)arg.Content[1]).Content[1]);
                        _nodes.Add(new ExprNode("NamedArgument", _nodes.Last().Type, new List<ITypeNode>() {
                            new IdentifierNode(name, new SimpleType(), false)
                        }));
                        PushForward();

                        argsList.Add(new ParameterValue(name, _nodes.Last().Type));
                    }
                }
            }

            if (argsList.Where(x => x.HasName).GroupBy(x => x.Name).Any(x => x.Count() > 1))
                throw new SemanticException("Unable to initialize named argument with two different values", node.Position);
            return argsList;
        }
    }
}
