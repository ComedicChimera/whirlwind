using System.Collections.Generic;
using System.Linq;

using Whirlwind.Types;

namespace Whirlwind.Semantic
{
    interface ITypeNode
    {
        IDataType Type { get; set; }
        string Name { get; }
    }

    class TreeNode
    {
        public List<ITypeNode> Nodes;
    }

    // operating expr node
    class ExprNode : TreeNode, ITypeNode
    {
        public string Name { get; }
        public IDataType Type { get; set; }

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
        public IDataType Type { get; set; }

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
        public IDataType Type { get; set; }

        public readonly string IdName;
        public readonly bool Constant;

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

    // stores constexpr identifiers
    class ConstexprNode : ITypeNode
    {
        public string Name { get { return "Constexpr"; } }
        public IDataType Type { get; set; }

        public readonly string IdName;
        public readonly string ConstValue;

        public ConstexprNode(string name, IDataType type, string constVal)
        {
            IdName = name;
            Type = type;
            ConstValue = constVal;
        }

        public override string ToString()
        {
            return $"ConstexprID({IdName}, {Type}, {ConstValue})";
        }
    }

    // stores statements
    class StatementNode : TreeNode, ITypeNode
    {
        public string Name { get; }
        public IDataType Type { get; set; }

        public StatementNode(string name)
        {
            Name = name;
            Type = new SimpleType();
            Nodes = new List<ITypeNode>();
        }

        public StatementNode(string name, List<ITypeNode> nodes)
        {
            Name = name;
            Type = new SimpleType();
            Nodes = nodes;
        }
    }
}
