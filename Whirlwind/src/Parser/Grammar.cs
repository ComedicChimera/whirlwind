using System;
using System.Collections.Generic;

namespace Whirlwind.Parser
{
    // The IGrammatical Interface
    // Used for storing the grammatical
    // elements of the Grammar Object
    interface IGrammatical
    {
        string Type();
    }

    // The Terminal Class
    // Represents a grammatical terminal
    sealed class Terminal : IGrammatical
    {
        public readonly string TokenType;
        
        public Terminal(string tokenType)
        {
            TokenType = tokenType;
        }

        public string Type() => "TERMINAL";
    }

    // The Nonterminal Class
    // Represents a grammatical nonterminal
    sealed class Nonterminal : IGrammatical
    {
        public readonly string Name;

        public Nonterminal(string name)
        {
            Name = name;
        }

        public string Type() => "NONTERMINAL";
    }

    // The Production Class
    // Represents a production of the grammar
    // linked to by a nonterminal
    class Production : IGrammatical
    {
        private readonly string _type;
        public List<IGrammatical> Content;

        public Production(string type)
        {
            _type = type;
            Content = new List<IGrammatical>();
        }

        public Production(string type, List<IGrammatical> content)
        {
            _type = type;
            Content = content;
        }

        public string Type() => _type;
    }

    // The Grammar Class
    // This class represents the objectified
    // form of the grammar (serialized grammar)
    class Grammar
    {
        private Dictionary<string, Production> _productions;
        public string First
        {
            get; private set;
        }

        public Grammar() {
            _productions = new Dictionary<string, Production>();
        }

        public bool AddProduction(string nonterminal, Production production)
        {
            if ( _productions.ContainsKey(nonterminal))
                return false;
            else
            {
                if (_productions.Keys.Count == 0)
                    First = nonterminal;
                _productions.Add(nonterminal, production);
                return true;
            }
        }

        public bool Lookup(string nonterminal)
            => _productions.ContainsKey(nonterminal);

        public Production GetProduction(string nonterminal)
        {
            if (!Lookup(nonterminal))
            {
                Console.WriteLine(nonterminal);
                throw new GrammarException($"Grammar has no production \'{nonterminal}\'.");
            }  
            return _productions[nonterminal];
        }
    }
}
