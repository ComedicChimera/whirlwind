using System.Collections.Generic;
using System.Linq;
using System;

namespace Whirlwind.Semantic
{
    class SymbolTable
    {
        private class Scope
        {
            public Dictionary<string, Symbol> Symbols;
            public List<Scope> SubScopes;

            public Scope()
            {
                Symbols = new Dictionary<string, Symbol>();
                SubScopes = new List<Scope>();
            }

            public bool AddSymbol(Symbol symbol)
            {
                if (Symbols.ContainsKey(symbol.Name))
                    return false;
                Symbols.Add(symbol.Name, symbol);
                return true;
            }

            public bool AddSymbol(Symbol symbol, int[] scopePath)
            {
                if (scopePath.Length == 1)
                    return SubScopes[scopePath[0]].AddSymbol(symbol);
                else
                    return SubScopes[scopePath[0]].AddSymbol(symbol, scopePath.Skip(1).ToArray());
            }

            public void ReplaceSymbol(string name, Symbol newSymbol)
            {
                Symbols[name] = newSymbol;
            }

            public void ReplaceSymbol(string name, Symbol newSymbol, int[] scopePath)
            {
                if (scopePath.Length == 1)
                    SubScopes[scopePath[0]].ReplaceSymbol(name, newSymbol);
                else
                    SubScopes[scopePath[0]].ReplaceSymbol(name, newSymbol, scopePath.Skip(1).ToArray());
            }

            public void AddScope(int[] scopePath)
            {
                if (scopePath.Length == 1)
                    SubScopes[scopePath[0]].SubScopes.Add(new Scope());
                else
                    SubScopes[scopePath[0]].AddScope(scopePath.Skip(1).ToArray());
            }
        }

        private Scope _table;
        private int[] _scopePath;

        public SymbolTable()
        {
            _table = new Scope();
            _scopePath = new int[] { };
        }

        public SymbolTable(Dictionary<string, Symbol> table)
        {
            _table = new Scope
            {
                Symbols = table
            };

            _scopePath = new int[] { };
        }

        public bool AddSymbol(Symbol symbol)
        {
            if (_scopePath.Length == 0)
                return _table.AddSymbol(symbol);
            else
                return _table.AddSymbol(symbol, _scopePath);
        }

        public void AddScope()
        {
            if (_scopePath.Length == 0)
                _table.SubScopes.Add(new Scope());
            else
                _table.AddScope(_scopePath);
        }

        public void DescendScope()
        {
            Scope currentScope = _table;
            foreach (int pos in _scopePath)
            {
                currentScope = currentScope.SubScopes[pos];
            }

            Array.Resize(ref _scopePath, _scopePath.Length + 1);
            _scopePath[_scopePath.Length - 1] = currentScope.SubScopes.Count - 1;
        }

        public void AscendScope()
        {
            Array.Resize(ref _scopePath, _scopePath.Length - 1);
        }

        public bool MoveScope(int startPos, int endPos)
        {
            Scope currentScope = _table;
            foreach (int pos in _scopePath)
            {
                currentScope = currentScope.SubScopes[pos];
            }

            if (startPos >= currentScope.SubScopes.Count || endPos >= currentScope.SubScopes.Count)
                return false;

            currentScope.SubScopes[endPos].Symbols.Concat(currentScope.SubScopes[startPos].Symbols);
            currentScope.SubScopes[endPos].SubScopes.AddRange(currentScope.SubScopes[startPos].SubScopes);

            currentScope.SubScopes.RemoveAt(startPos);

            return true;
        }

        // remove the last scope in the table (used internally during template evaluation)
        public void RemoveScope()
        {
            Scope currentScope = _table;
            foreach (int pos in _scopePath)
            {
                currentScope = currentScope.SubScopes[pos];
            }

            currentScope.SubScopes.RemoveAt(currentScope.SubScopes.Count - 1);
        }

        // add the given scope to scope path (used only internally so no checking done)
        public void GotoScope(int pos)
        {
            Array.Resize(ref _scopePath, _scopePath.Length + 1);
            _scopePath[_scopePath.Length - 1] = pos;
        }

        public bool Lookup(string name, out Symbol symbol)
        {
            var visibleScopes = new List<Scope>() { _table };
            Scope currentScope = _table;
            foreach(int scopePos in _scopePath)
            {
                currentScope = currentScope.SubScopes[scopePos];
                visibleScopes.Add(currentScope);
            }
            visibleScopes.Reverse();
            foreach (Scope scope in visibleScopes)
            {
                if (scope.Symbols.ContainsKey(name))
                {
                    symbol = scope.Symbols[name];
                    return true;
                }
            }
            symbol = null;
            return false;
        }
        
        public Dictionary<string, Symbol> Filter(Func<Symbol, bool> compareFunc)
        {
            return _table.Symbols.Where(x => compareFunc(x.Value)).Select(x => x.Value).ToDictionary(x => x.Name);
        }

        // no need for boolean since this is only called internally
        public void ReplaceSymbol(string name, Symbol newSymbol)
        {
            if (_scopePath.Length == 0)
                _table.ReplaceSymbol(name, newSymbol);
            else
                _table.ReplaceSymbol(name, newSymbol, _scopePath);
        }

        public List<Symbol> GetScope()
        {
            Scope scope = _table;

            foreach (int p in _scopePath)
                scope = scope.SubScopes[p];

            return scope.Symbols.Values.ToList();
        }

        public int GetScopeCount()
        {
            Scope scope = _table;

            foreach (int p in _scopePath)
                scope = scope.SubScopes[p];

            return scope.SubScopes.Count;
        }
    }
}
