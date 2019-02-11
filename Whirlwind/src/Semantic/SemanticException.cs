using System;

using Whirlwind.Parser;

namespace Whirlwind.Semantic
{
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
}
