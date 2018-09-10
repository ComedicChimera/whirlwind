using System.Collections.Generic;
using Whirlwind.Types;

namespace Whirlwind.Semantic
{
    enum Modifier
    {
        PRIVATE,
        PARTIAL,
        EXPORTED,
        PROTECTED,
        STATIC,
        CONSTANT,
        CONSTEXPR,
        VOLATILE
    }

    class Package : IDataType
    {
        public readonly SymbolTable ExternalTable;
        public readonly string Name;
        public bool Used;

        public Package(SymbolTable eTable, string name, bool used = false)
        {
            ExternalTable = eTable;
            Name = name;
            Used = used;
        }

        public bool Coerce(IDataType _) => false;
        public TypeClassifier Classify() => TypeClassifier.PACKAGE;
    }

    class Symbol
    {
        public readonly string Name;
        public readonly IDataType DataType;
        public readonly List<Modifier> Modifiers;

        public readonly string Value;

        public Symbol(string name, IDataType dt)
        {
            Name = name;
            DataType = dt;
            Modifiers = new List<Modifier>();
        }

        public Symbol(string name, IDataType dt, List<Modifier> modifiers)
        {
            Name = name;
            DataType = dt;
            Modifiers = modifiers;
        }

        public Symbol(string name, IDataType dt, string value)
        {
            Name = name;
            DataType = dt;
            Modifiers = new List<Modifier> { Modifier.CONSTEXPR };
            Value = value;
        }

        public Symbol(string name, IDataType dt, List<Modifier> modifiers, string value)
        {
            Name = name;
            DataType = dt;
            Modifiers = modifiers;
            Value = value;
        }
    }
}
