using System.Collections.Generic;
using System.Linq;

using Whirlwind.Types;

namespace Whirlwind.Semantic
{
    interface ITypeNode
    {
        IDataType Type { get; }
        string Name { get; }
    }

    // operating expr node
    class ExprNode : ITypeNode
    {
        public string Name { get; }
        public IDataType Type { get; }

        public readonly List<ITypeNode> Nodes;

        public ExprNode(string name, IDataType type)
        {
            Name = name;
            Type = type;
            Nodes = new List<ITypeNode>();
        }

        public ExprNode(string name, IDataType type, List<ITypeNode> nodes)
        {
            Name = name;
            Type = type;
            Nodes = nodes;
        }

        public override string ToString()
        {
            return $"{Name}:[{string.Join(", ", Nodes.Select(x => x.ToString()))}]";
        }
    }

    // stores literal values
    class ValueNode : ITypeNode
    {
        public string Name { get; }
        public IDataType Type { get; }

        public string Value;

        public ValueNode(string name, IDataType type)
        {
            Name = name;
            Type = type;
        }

        public ValueNode(string name, IDataType type, string value)
        {
            Name = name;
            Type = type;
            Value = value;
        }

        public override string ToString()
        {
            return $"Value({Name}, {Type}, {Value})";
        }
    }

    // stores identifiers
    class IdentifierNode : ITypeNode
    {
        public string Name { get { return "Identifier"; } }
        public IDataType Type { get; }

        public readonly string IdName;
        public readonly bool Constant;
        public readonly bool Constexpr;
        public readonly string ConstValue;

        public IdentifierNode(string name, IDataType type, bool constant)
        {
            IdName = name;
            Type = type;
            Constant = constant;
            Constexpr = false;
        }

        public IdentifierNode(string name, IDataType type, string constValue)
        {
            IdName = name;
            Type = type;
            Constant = true;
            Constexpr = true;
            ConstValue = constValue;
        }

        public override string ToString()
        {
            return $"{(Constant ? "Constant" : "Identifier")}({IdName}, {Type})";
        }
    }
}
