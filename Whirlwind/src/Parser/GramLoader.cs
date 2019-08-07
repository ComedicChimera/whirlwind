using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;

namespace Whirlwind.Parser
{
    // The GrammarException Class
    // This exception is thrown whenever
    // the input grammar is malformed
    class GrammarException : Exception
    {
        public Token Tok;
        public bool IsToken = false;
        public string ErrorMessage;

        public GrammarException(Token token)
        {
            IsToken = true;
            Tok = token;
        }

        public GrammarException(string errorMessage)
        {
            ErrorMessage = errorMessage;
        }
    }

    // The GramLoader Class
    // This class is responsible for converting the
    // string grammar into a Grammar Object to be
    // used by the WhirlParser
    class GramLoader
    {
        private Scanner _scanner = new Scanner("config/ebnf.json");
        private List<Token> _tokens;
        private int _tokenPos = 0;
        private bool _done;

        private Grammar grammar = new Grammar();

        private Token CurrentToken
        {
            get {
                if (_done)
                    return new Token("EOF", "$", -1);
                return _tokens[_tokenPos];
            }
        }

        private void Next(int amount = 1)
        {
            if (_tokenPos + amount < _tokens.Count)
                _tokenPos += amount;
            else
                _done = true;
        }

        private Token LookAhead(int amount = 0)
        {
            if (_tokenPos + amount < _tokens.Count && _tokenPos + amount > -1)
                return _tokens[_tokenPos + amount];
            else
                return new Token("EOF", "$", -1);
        }

        public Grammar Load(string path)
        {
            string text = File.ReadAllText(path);
            _tokens = _scanner.Scan(text);
            return _parseGrammar();
        }

        private Grammar _parseGrammar()
        {      
            while (!_done)
                _parseNextProduction();
            return grammar;
        }

        private void _parseNextProduction()
        {
            string name;
            if (CurrentToken.Type != "NONTERMINAL")
                throw new GrammarException(CurrentToken);
            else
                name = CurrentToken.Value;
            Next();
            if (CurrentToken.Type != ":")
                throw new GrammarException(CurrentToken);
            Next();
            var prod = _parseProductionContent();
            if (CurrentToken.Type != ";")
                throw new GrammarException(CurrentToken);
            if (!grammar.AddProduction(name, prod))
                throw new GrammarException(name);
            Next();
        }

        private Production _parseProductionContent()
        {
            var production = new Production("GROUP");
            bool collecting = true;
            while (collecting)
            {
                switch (CurrentToken.Type)
                {
                    case "(":
                        Next();
                        production.Content.Add(_parseProductionContent());
                        Next();
                        break;
                    case "[":
                        Next();
                        var subPro = new Production("OPTIONAL", new List<IGrammatical>() { _parseProductionContent() });
                        production.Content.Add(subPro);
                        Next();
                        break;
                    case "TERMINAL":
                        production.Content.Add(new Terminal(CurrentToken.Value.Substring(1, CurrentToken.Value.Length - 2)));
                        Next();
                        break;
                    case "NONTERMINAL":
                        production.Content.Add(new Nonterminal(CurrentToken.Value));
                        Next();
                        break;
                    case "+":
                    case "*":
                        var lastProd = production.Content.Last();
                        production.Content.RemoveLast();
                        production.Content.Add(new Production(CurrentToken.Type, new List<IGrammatical>() { lastProd }));
                        Next();
                        break;
                    default:
                        collecting = false;
                        break;
                }
            }
            if (CurrentToken.Type == "|")
            {
                production = new Production("ALTERNATOR", new List<IGrammatical>() { production });
                Next();
                var nPro = _parseProductionContent();
                if (nPro.Type() == "ALTERNATOR")
                    production.Content = production.Content.Concat(nPro.Content).ToList();
                else
                    production.Content.Add(nPro);
            }
            return production;
        }
    }
}
