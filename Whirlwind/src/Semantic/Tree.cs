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

    class TreeNode : ITypeNode
    {
        public string Name { get; }
        public IDataType Type { get; }

        public readonly List<ITypeNode> Nodes;

        public TreeNode(string name, IDataType type)
        {
            Name = name;
            Type = type;
            Nodes = new List<ITypeNode>();
        }

        public TreeNode(string name, IDataType type, List<ITypeNode> nodes)
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

    class IdentifierNode : ITypeNode
    {
        public string Name { get { return "Identifier"; } }
        public IDataType Type { get; }

        public string IdName { get; }
        public bool Constant { get; }

        public IdentifierNode(string name, IDataType type, bool constant)
        {
            IdName = name;
            Type = type;
            Constant = constant;
        }

        public override string ToString()
        {
            return $"{(Constant ? "Constant" : "Identifier")}({IdName}, {Type})";
        }
    }
}
