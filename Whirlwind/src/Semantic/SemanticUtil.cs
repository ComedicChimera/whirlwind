using System;
using System.Collections.Generic;

using Whirlwind.Parser;
using Whirlwind.Types;

namespace Whirlwind.Semantic
{
    // semantic exception
    class SemanticException : Exception
    {
        public readonly TextPosition Position;
        public readonly new string Message;

        public SemanticException(string message, TextPosition pos)
        {
            Message = message;
            Position = pos;
        }
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
