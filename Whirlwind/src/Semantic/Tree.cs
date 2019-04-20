using System.Collections.Generic;
using System.Linq;
using System;

using Whirlwind.Types;
using Whirlwind.Parser;

namespace Whirlwind.Semantic
{
    interface ITypeNode
    {
        DataType Type { get; set; }
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
        public DataType Type { get; set; }

        public ExprNode(string name, DataType type)
        {
            Name = name;
            Type = type;
            Nodes = new List<ITypeNode>();
        }

        public ExprNode(string name, DataType type, List<ITypeNode> nodes)
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
        public DataType Type { get; set; }

    public string Value;

        public ValueNode(string name, DataType type)
        {
            Name = name;
            Type = type;
        }

        public ValueNode(string name, DataType type, string value)
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
        public DataType Type { get; set; }

        public readonly string IdName;

        public IdentifierNode(string name, DataType type)
        {
            IdName = name;
            Type = type;
        }

        public override string ToString()
        {
            return $"{(Type.Constant ? "Constant" : "Identifier")}({IdName}, {Type})";
        }
    }

    // stores constexpr identifiers
    class ConstexprNode : ITypeNode
    {
        public string Name { get { return "Constexpr"; } }
        public DataType Type { get; set; }

        public readonly string IdName;
        public readonly string ConstValue;

        public ConstexprNode(string name, DataType type, string constVal)
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
        public DataType Type { get; set; }

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

        public override string ToString()
        {
            return $"{Name}:[{string.Join(", ", Nodes.Select(x => x.ToString()))}]";
        }
    }

    class BlockNode : TreeNode, ITypeNode
    {
        public string Name { get; }
        public DataType Type { get; set; }

        public List<ITypeNode> Block;

        public BlockNode(string name)
        {
            Name = name;
            Nodes = new List<ITypeNode>();
            Block = new List<ITypeNode>();
        }

        public override string ToString()
        {
            return $"{Name}:[{string.Join(", ", Nodes.Select(x => x.ToString()))}] " +
                $"{{\n\t{string.Join(", ", Block.Select(x => x.ToString()))}\n}}";
        }
    }

    class IncompleteNode : ITypeNode
    {
        public readonly ASTNode AST;

        public IncompleteNode(ASTNode ast)
        {
            AST = ast;
        }

        public string Name
        {
            get { return "INCOMPLETE"; }
        }

        public DataType Type
        {
            get { return new SimpleType(); }
            set { throw new NotImplementedException(); }
        }
    }
}
