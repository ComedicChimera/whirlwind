using System.Collections.Generic;
using System.Linq;

// remove to string methods, when they are no longer being used
namespace Whirlwind.Parser
{
    interface INode
    {
        string Name();
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
    }
}
