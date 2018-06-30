using System;
using System.Collections.Generic;
using System.Text;
using System.IO;

namespace Shadow.Parser
{
    interface IGrammatical
    {
        string Type();
    }

    class Terminal : IGrammatical
    {
        private string TokenType;
        
        public Terminal(string tokenType)
        {
            TokenType = tokenType;
        }

        public string Type() => "TERMINAL";
    }

    class Nonterminal : IGrammatical
    {
        public string Name;

        public Nonterminal(string name)
        {
            Name = name;
        }

        public string Type() => "NONTERMINAL";
    }

    class Production : IGrammatical
    {
        private string _type;
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

    class Grammar
    {
        private Dictionary<string, Production> _productions;

        public bool AddProduction(string nonterminal, Production production)
        {
            if ( _productions.ContainsKey(nonterminal))
            {
                return false;
            }
            else
            {
                _productions.Add(nonterminal, production);
                return true;
            }
        }

        public bool Lookup(string nonterminal)
        {
            return _productions.ContainsKey(nonterminal);
        }
    }
}
