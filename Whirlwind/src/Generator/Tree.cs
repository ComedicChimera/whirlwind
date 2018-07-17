using System.Collections.Generic;
using Whirlwind.Types;

namespace Whirlwind.Generator
{
    interface ITypeNode
    {
        string Name();
        IDataType Type();
    }

    class TreeNode : ITypeNode
    {
        private readonly string _name;
        private readonly IDataType _type;

        public readonly List<ITypeNode> Nodes;

        public TreeNode(string name, IDataType type)
        {
            _name = name;
            _type = type;
        }

        public TreeNode(string name, IDataType type, List<ITypeNode> nodes)
        {
            _name = name;
            _type = type;
            Nodes = nodes;
        }

        public string Name() => _name;
        public IDataType Type() => _type;
    }

    class ValueNode : ITypeNode
    {
        private readonly string _name;
        private readonly IDataType _type;

        public string Value;

        public ValueNode(string name, IDataType type)
        {
            _name = name;
            _type = type;
        }

        public ValueNode(string name, IDataType type, string value)
        {
            _name = name;
            _type = type;
            Value = value;
        }

        public string Name() => _name;
        public IDataType Type() => _type;
    }
}
