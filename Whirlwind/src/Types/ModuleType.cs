using Whirlwind.Generator;

using System;
using System.Linq;
using System.Collections.Generic;

// add partials and interfaces
namespace Whirlwind.Types
{
    class ModuleInstance : IDataType
    {
        private readonly List<Symbol> _instance;

        public readonly string Name;
        public readonly List<IDataType> Inherits;

        public ModuleInstance(string name, SymbolTable table, List<IDataType> inherits)
        {
            Name = name;
            _instance = table.Filter(Modifier.PROPERTY).Where(x => !x.Modifiers.Contains(Modifier.PRIVATE)).ToList();
            Inherits = inherits;
        }

        public bool GetProperty(string name, out Symbol symbol)
        {
            if (_instance.Select(x => x.Name).Contains(name))
            {
                symbol = _instance.Where(x => x.Name == name).First();
                return true;
            }
            symbol = null;
            return false;
        }

        public string Classify() => "MODULE_INSTANCE";

        public bool Coerce(IDataType other)
        {
            return this == other;
        }
    }

    class ModuleType : IDataType
    {
        private readonly SymbolTable _table;
        private readonly List<Tuple<FunctionType, TreeNode>> _constructors;

        private readonly string Name;
        public readonly List<IDataType> Inherits;
        public bool Partial;

        public ModuleType(string name, bool partial)
        {
            Name = name;
            _table = new SymbolTable();
            _constructors = new List<Tuple<FunctionType, TreeNode>>();
            Inherits = new List<IDataType>();
            Partial = partial;
        }

        public bool AddConstructor(FunctionType ft, TreeNode body)
        {
            if (_constructors.Where(x => x.Item1.Coerce(ft)).Count() != 0)
                return false;
            _constructors.Add(new Tuple<FunctionType, TreeNode>(ft, body));
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
                if (!foundSymbol.Modifiers.Contains(Modifier.PRIVATE) && !foundSymbol.Modifiers.Contains(Modifier.PROPERTY))
                {
                    member = foundSymbol;
                    return true;
                }
            }
            member = null;
            return false;
        }

        public bool GetConstructor(List<ParameterValue> parameters)
        {
            return _constructors.Where(x => x.Item1.MatchParameters(parameters)).Count() > 0;
        }

        public ModuleInstance GetInstance()
        {
            return new ModuleInstance(Name, _table, Inherits);
        }

        public string Classify() => "MODULE";

        public bool Coerce(IDataType dt)
        {
            return false;
        }
    }
}
