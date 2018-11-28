using Whirlwind.Semantic;

using System;
using System.Linq;
using System.Collections.Generic;

// add partials and interfaces
namespace Whirlwind.Types
{
    class ObjectInstance : IDataType
    {
        private readonly Dictionary<string, Symbol> _instance;

        public readonly string Name;
        public readonly List<IDataType> Inherits;

        public ObjectInstance(string name, SymbolTable table, List<IDataType> inherits, bool internalInstance)
        {
            Name = name;
            if (internalInstance)
                _instance = table.Filter(x => !x.Modifiers.Contains(Modifier.STATIC));
            else
                _instance = table.Filter(x => !x.Modifiers.Contains(Modifier.STATIC) && !x.Modifiers.Any(y => new[] { Modifier.PRIVATE, Modifier.PROTECTED }.Contains(y)));
            Inherits = inherits;
        }

        public bool GetProperty(string name, out Symbol symbol)
        {
            if (_instance.ContainsKey(name))
            {
                symbol = _instance[name];
                return true;
            }
            symbol = null;
            return false;
        }

        public List<string> ListProperties() => _instance.Keys.ToList();

        public TypeClassifier Classify() => TypeClassifier.OBJECT_INSTANCE;

        public bool Coerce(IDataType other)
        {
            if (other.Classify() == TypeClassifier.OBJECT_INSTANCE)
                // name makes the distinction
                return Name == ((ObjectInstance)other).Name;

            return false;
        }

        public bool Equals(IDataType other) => Coerce(other);
    }

    class ObjectType : IDataType
    {
        private readonly SymbolTable _table;
        private readonly List<Tuple<FunctionType, ExprNode>> _constructors;

        public readonly string Name;
        public readonly List<IDataType> Inherits;
        public bool Partial;

        public ObjectType(string name, bool partial)
        {
            Name = name;
            _table = new SymbolTable();
            _constructors = new List<Tuple<FunctionType, ExprNode>>();
            Inherits = new List<IDataType>();
            Partial = partial;
        }

        public bool AddConstructor(FunctionType ft, ExprNode body)
        {
            if (_constructors.Where(x => x.Item1.Coerce(ft)).Count() != 0)
                return false;
            _constructors.Add(new Tuple<FunctionType, ExprNode>(ft, body));
            return true;
        }

        public bool AddInherits(IDataType dt)
        {
            if (Inherits.Contains(dt))
            {
                Inherits.Add(dt);
                return true;
            }
            return false;
        }

        public bool AddMember(Symbol member)
        {
            return _table.AddSymbol(member);
        }

        public bool GetMember(string name, out Symbol member)
        {
            if (_table.Lookup(name, out Symbol foundSymbol))
            {
                if (!foundSymbol.Modifiers.Contains(Modifier.PRIVATE) && foundSymbol.Modifiers.Contains(Modifier.STATIC))
                {
                    member = foundSymbol;
                    return true;
                }
            }
            member = null;
            return false;
        }

        public bool GetConstructor(List<IDataType> parameters, out FunctionType constructor)
        {
            var constructors = _constructors.Where(x => x.Item1.MatchParameters(parameters)).ToList();
            
            if (constructors.Count() == 1)
            {
                constructor = constructors[0].Item1;
                return true;
            }
            constructor = null;
            return false;
        }

        public ObjectInstance GetInstance()
        {
            return new ObjectInstance(Name, _table, Inherits, false);
        }

        public ObjectInstance GetInternalInstance()
        {
            return new ObjectInstance(Name, _table, Inherits, true);
        }

        public TypeClassifier Classify() => TypeClassifier.OBJECT_INSTANCE;

        public bool Coerce(IDataType dt) => false;

        public bool Equals(IDataType dt) => false;
    }
}
