using System;
using System.Collections.Generic;
using System.Linq;

// remove to string methods, when they are no longer being used
namespace Whirlwind.Parser
{
    struct TextPosition
    {
        public int Start, Length;

        public TextPosition(int start, int len)
        {
            Start = start;
            Length = len;
        }
    }

    interface INode
    {
        string Name();
        TextPosition Position { get; }
    }

    class TokenNode : INode
    {
        public readonly Token Tok;

        public TokenNode(Token token)
        {
            Tok = token;
        }

        public string Name() => "TOKEN";

        public override string ToString()
        {
            return $"Token({Tok.Type}, {Tok.Value})";
        }

        public TextPosition Position => new TextPosition(Tok.Index, Tok.Value.Length);
    }

    class ASTNode : INode
    {
        private readonly string _name;
        public List<INode> Content;

        public ASTNode(string name)
        {
            _name = name;
            Content = new List<INode>();
        }

        public string Name() => _name;

        public override string ToString()
        {
            return $"{_name}:[{string.Join(", ", Content.Select(x => x.ToString()))}]";
        }

        public TextPosition Position
        {
            get
            {
                int start = Content.First().Position.Start;
                var end = Content.Last().Position;
                return new TextPosition(start, (end.Start + end.Length) - start);
            }
        }
    }
}
