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
        STATIC
    }

    class Package : DataType
    {
        public readonly SymbolTable ExternalTable;
        public readonly string Name;

        public Package(SymbolTable eTable, string name)
        {
            ExternalTable = eTable;
            Name = name;

            // WATCH CLOSELY
            Constant = true;
        }

        public override bool Coerce(DataType _) => false;
        public override TypeClassifier Classify() => TypeClassifier.PACKAGE;

        protected override bool _equals(DataType other)
        {
            if (other.Classify() == TypeClassifier.PACKAGE)
                return Name == ((Package)other).Name;

            return false;
        }

        public override DataType ConstCopy()
            => new Package(ExternalTable, Name); // implicit const
    }

    class Symbol
    {
        public readonly string Name;

        public DataType DataType;
        public List<Modifier> Modifiers;

        public readonly string Value;

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

        public Symbol(string name, DataType dt, string value)
        {
            Name = name;
            DataType = dt;
            Modifiers = new List<Modifier> { Modifier.CONSTEXPR };
            Value = value;
        }

        public Symbol(string name, DataType dt, List<Modifier> modifiers, string value)
        {
            Name = name;
            DataType = dt;
            Modifiers = modifiers;
            Value = value;
        }

        public bool Equals(Symbol other)
        {
            if (Name == other.Name && DataType.Equals(other.DataType) && Value == other.Value)
            {
                return Modifiers.Count == other.Modifiers.Count
                    && Enumerable.Range(0, Modifiers.Count).All(i => Modifiers[i] == other.Modifiers[i]);
            }

            return false;
        }
    }
}
