using System;
using System.Collections.Generic;

using Whirlwind.Syntax;
using Whirlwind.Types;

namespace Whirlwind.Semantic
{
    // semantic exception
    class SemanticException : Exception
    {
        public readonly new string Message;
        public readonly TextPosition Position;

        public string FileName;

        public SemanticException(string message, TextPosition pos)
        {
            Message = message;
            Position = pos;
            FileName = "";
        }
    }

    // exception thrown when inductive inferencing for type classes
    // is enabled and an identifier lookup fails
    class SemanticContextException : Exception
    {
        
    }

    // exception thrown when a node containing an incomplete self-referential
    // type is used in an expression (cause in _visitExpr)
    class SemanticSelfIncompleteException : Exception
    {

    }

    // stores arguments that are passed in to a given function
    class ArgumentList
    {
        public readonly List<DataType> UnnamedArguments;
        public readonly Dictionary<string, DataType> NamedArguments;

        public ArgumentList()
        {
            UnnamedArguments = new List<DataType>();
            NamedArguments = new Dictionary<string, DataType>();
        }

        public ArgumentList(List<DataType> args)
        {
            UnnamedArguments = args;
            NamedArguments = new Dictionary<string, DataType>();
        }

        public ArgumentList(List<DataType> _uArgs, Dictionary<string, DataType> _nArgs)
        {
            UnnamedArguments = _uArgs;
            NamedArguments = _nArgs;
        }

        public int Count() => UnnamedArguments.Count + NamedArguments.Count;
    }
}
