using System;
using System.IO;
using System.Collections.Generic;

namespace Shadow.Parser
{
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

    class GramLoader
    {
        private Scanner _scanner = new Scanner("Config/ebnf.json");
        private List<Token> _tokens;
        private int _tokenPos = 0;
        private bool _done;

        private Grammar grammar = new Grammar();

        private Token CurrentToken
        {
            get { return _tokens[_tokenPos]; }
        }

        private void Next(int amount = 0)
        {
            if (_tokenPos + amount < _tokens.Count)
            {
                _tokenPos += amount;
            }
            else
            {
                _done = true;
            }
        }

        private Token LookAhead(int amount = 0)
        {
            if (_tokenPos + amount < _tokens.Count && _tokenPos + amount > -1)
            {
                return _tokens[_tokenPos + amount];
            }
            else
            {
                return new Token("EOF", "$", -1);
            }
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
            {
                _parseNextProduction();
            }
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
            if (!grammar.AddProduction(name, prod))
                throw new GrammarException(name);
        }

        private Production _parseProductionContent()
        {
            var production = new Production("GROUP");
            switch (CurrentToken.Type)
            {
                case "(":
                    Next();
                    production.Content.Add(_parseProductionContent());
                    Next();
                    break;
                case "[":
                    Next();
                    var subPro = new Production("OPTIONAL", _parseProductionContent().Content);
                    production.Content.Add(subPro);
                    Next();
                    break;
                case "TERMINAL":
                    production.Content.Add(new Terminal(CurrentToken.Value));
                    break;
                case "NONTERMINAL":
                    production.Content.Add(new Nonterminal(CurrentToken.Value));
                    break;
                default:
                    throw new GrammarException(CurrentToken);
            }
            return production;
        }
    }
}
