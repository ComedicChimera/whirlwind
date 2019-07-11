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
        string Name { get; }
        TextPosition Position { get; }
    }

    class TokenNode : INode
    {
        public readonly Token Tok;
        public string Name { get { return "TOKEN"; } }

        public TokenNode(Token token)
        {
            Tok = token;
        }

        public override string ToString()
        {
            return $"Token({Tok.Type}, {Tok.Value})";
        }

        public TextPosition Position => new TextPosition(Tok.Index, Tok.Value.Length);
    }

    class ASTNode : INode
    {
        public string Name { get; }
        public List<INode> Content;

        public ASTNode(string name)
        {
            Name = name;
            Content = new List<INode>();
        }

        public override string ToString()
        {
            return $"{Name}:[{string.Join(", ", Content.Select(x => x.ToString()))}]";
        }

        public TextPosition Position
        {
            get
            {
                if (Content.Count == 1)
                    return Content[0].Position;

                int start = Content.First().Position.Start;
                var end = Content.Last().Position;
                return new TextPosition(start, (end.Start + end.Length) - start);
            }
        }
    }
}
