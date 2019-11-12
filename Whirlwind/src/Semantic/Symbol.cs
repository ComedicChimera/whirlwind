using System.Collections.Generic;
using System.Linq;

using Whirlwind.Types;

namespace Whirlwind.Semantic
{
    enum Modifier
    {
        EXPORTED,
        CONSTEXPR,
        VOLATILE,
        STATIC,
        OWNED,
        MUTABLE
    }

    class PackageType : DataType
    {
        public readonly Dictionary<string, Symbol> ExternalTable;

        public PackageType(Dictionary<string, Symbol> eTable)
        {
            ExternalTable = eTable;

            // WATCH CLOSELY
            Constant = true;
        }

        public bool Lookup(string name, out Symbol symbol)
        {
            if (ExternalTable.ContainsKey(name))
            {
                symbol = ExternalTable[name];
                return true;
            }

            symbol = null;
            return false;
        }

        public override bool Coerce(DataType _) => false;
        public override TypeClassifier Classify() => TypeClassifier.PACKAGE;

        public override DataType ConstCopy()
            => new PackageType(ExternalTable); // implicit const

        protected override bool _equals(DataType other) => false;
    }

    class Symbol
    {
        public readonly string Name;

        public DataType DataType;
        public List<Modifier> Modifiers;

        public readonly ITypeNode Value;

        public Symbol(string name, DataType dt)
        {
            Name = name;
            DataType = dt;
            Modifiers = new List<Modifier>();
        }

        public Symbol(string name, DataType dt, List<Modifier> modifiers)
        {
            Name = name;
            DataType = dt;
            Modifiers = modifiers;
        }

        public Symbol(string name, DataType dt, ITypeNode value)
        {
            Name = name;
            DataType = dt;
            Modifiers = new List<Modifier> { Modifier.CONSTEXPR };
            Value = value;
        }

        public Symbol(string name, DataType dt, List<Modifier> modifiers, ITypeNode value)
        {
            Name = name;
            DataType = dt;
            Modifiers = modifiers;
            Value = value;
        }

        public bool Equals(Symbol other)
        {
            if (Name == other.Name && DataType.Equals(other.DataType))
                return Modifiers.EnumerableEquals(other.Modifiers);

            return false;
        }

        public override bool Equals(object obj)
        {
            if (obj is Symbol sym && obj != null)
                return Equals(sym);

            return base.Equals(obj);
        }

        // forcibly create a new modifier list
        public Symbol Copy()
            => new Symbol(Name, DataType.Copy(), Modifiers.Select(x => x).ToList(), Value);
    }
}
