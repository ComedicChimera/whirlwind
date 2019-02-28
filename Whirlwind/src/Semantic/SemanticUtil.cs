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
        public readonly List<IDataType> UnnamedArguments;
        public readonly Dictionary<string, IDataType> NamedArguments;

        public ArgumentList()
        {
            UnnamedArguments = new List<IDataType>();
            NamedArguments = new Dictionary<string, IDataType>();
        }

        public ArgumentList(List<IDataType> _uArgs, Dictionary<string, IDataType> _nArgs)
        {
            UnnamedArguments = _uArgs;
            NamedArguments = _nArgs;
        }

        public int Count() => UnnamedArguments.Count + NamedArguments.Count;
    }
}
