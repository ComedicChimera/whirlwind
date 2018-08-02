using Whirlwind.Semantic;

using static Whirlwind.Semantic.Checker.Checker;

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

    class MethodGroup : IDataType
    {
        private readonly List<FunctionType> _methods;

        public MethodGroup(List<FunctionType> methods)
        {
            _methods = methods;
        }

        public bool AddMethod(FunctionType method)
        {
            if (_methods.Select(x => Different(x, method)).All(x => x))
            {
                _methods.Add(method);
            }
            return false;
        }

        public bool GetMember(List<ParameterValue> parameters, out FunctionType method)
        {
            var matches = _methods.Where(x => CheckParameters(x, parameters)).ToArray();
            if (matches.Length == 1)
            {
                method = matches[0];
                return true;
            }

            method = null;
            return false;
        }

        // add checks for edge cases if possible
        public static bool Different(FunctionType a, FunctionType b)
        {
            using (var e1 = a.Parameters.GetEnumerator())
            using (var e2 = b.Parameters.GetEnumerator())
            {
                while (e1.MoveNext() && e2.MoveNext())
                {
                    if (e1.Current.Indefinite && e2.Current.Indefinite)
                        return false;
                    if (e1.Current.Optional && e2.Current.Optional)
                        return false;
                    if (e1.Current.DataType != e2.Current.DataType)
                    {
                        // add checks for edge cases if possible
                        return true;
                    }
                    
                }
            }

            if (a.Parameters.Where(x => !x.Optional && !x.Indefinite).Count() != b.Parameters.Where(x => !x.Optional && !x.Indefinite).Count())
                return true;
            return false;
        }

        public string Classify() => "METHOD_GROUP";
        public bool Coerce(IDataType other) => false;
    }

    class ModuleType : IDataType
    {
        private readonly SymbolTable _table;
        private readonly List<Tuple<FunctionType, TreeNode>> _constructors;

        public readonly string Name;
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
            if (!_table.AddSymbol(member))
            {
                if (member.DataType.Classify() != "FUNCTION")
                    return false;
                _table.Lookup(member.Name, out Symbol symbol);
                switch (symbol.DataType.Classify())
                {
                    case "METHOD_GROUP":
                        if (((MethodGroup)symbol.DataType).AddMethod((FunctionType)member.DataType))
                        {
                            _table.ReplaceSymbol(member.Name, symbol);
                            return true;
                        }
                        return false;
                    case "FUNCTION":
                        if (MethodGroup.Different((FunctionType)symbol.DataType, (FunctionType)member.DataType))
                        {
                            _table.ReplaceSymbol(member.Name, new Symbol(member.Name, new MethodGroup(new List<FunctionType>() {
                                (FunctionType)member.DataType,
                                (FunctionType)symbol.DataType
                            })));
                            return true;
                        }
                        return false;
                    default:
                        return false;
                }
            }
            return true;
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

        public bool GetConstructor(List<ParameterValue> parameters, out FunctionType constructor)
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
