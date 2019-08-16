using System;
using System.Collections.Generic;
using System.Linq;

namespace Whirlwind.Syntax
{
    // The InvalidSyntaxException Class
    // Thrown whenever the parser encounters
    // a series of tokens it cannot parse
    class InvalidSyntaxException : Exception
    {
        public Token Tok;

        public InvalidSyntaxException(Token token)
        {
            Tok = token;
        }
    }

    // The Parser Class
    // The class responsible to taking the Grammar object
    // and the list of Tokens from the Scanner
    // and turning it into an AST while simultaneously
    // checking it for valid syntax
    sealed class Parser
    {
        private readonly Grammar _grammar;
        private List<Token> _tokens;
        private readonly List<ASTNode> _semanticStack;
        private int _errorPosition = -1;

        public Parser(Grammar grammar)
        {
            _grammar = grammar;
            _semanticStack = new List<ASTNode>();
        }

        public ASTNode Parse(List<Token> tokens)
        {
            _tokens = tokens;

            // error position is position of token in THE TOKENS LIST, not the source string            
            if (_parseProduction(_grammar.First) != _tokens.Count)
                throw new InvalidSyntaxException(GetToken(_errorPosition));

            var tree = _semanticStack[0];
            _semanticStack.Clear();

            _tokens.Clear();

            return tree;
        }

        private Token GetToken(int pos)
        {
            if (pos < _tokens.Count && pos > -1)
                return _tokens[pos];
            else
                return new Token("EOF", "$", -1);
        }

        private int _parseProduction(string name, int offset = 0)
        {
            var production = _grammar.GetProduction(name);
            _semanticStack.Add(new ASTNode(name));
            int ndx = offset;                

            if (production.Type() == "ALTERNATOR")
                ndx = _matchAll(production.Content, offset);
            else
                ndx = _match(production, offset);

            // clear empty or invalid trees
            if (ndx == -1 || ndx == offset)
                _semanticStack.RemoveLast();

            return ndx;
        }

        private int _match(Production production, int offset)
        {
            int localOffset = offset, cachedSize = _semanticStack.Last().Content.Count;
            foreach (var item in production.Content)
            {
                switch (item.Type())
                {
                    case "TERMINAL":
                        if (((Terminal)item).TokenType != GetToken(localOffset).Type)
                        {
                            if (_errorPosition < localOffset)
                                _errorPosition = localOffset;
                            goto CLEANUP;
                        }
                        _semanticStack.Last().Content.Add(new TokenNode(GetToken(localOffset)));
                        localOffset++;
                        break;
                    case "NONTERMINAL":
                        {
                            int newOffset = _parseProduction(((Nonterminal)item).Name, localOffset);
                            if (newOffset == -1)
                                goto CLEANUP;
                            // tree was empty
                            if (localOffset != newOffset)
                            {
                                _semanticStack[_semanticStack.Count - 2].Content.Add(_semanticStack.Last());
                                _semanticStack.RemoveLast();

                                localOffset = newOffset;
                            }
                            break;
                        }
                        
                    case "*":
                        {
                            int newOffset = _match((Production)item, localOffset);
                            while (newOffset != -1)
                            {
                                localOffset = newOffset;
                                newOffset = _match((Production)item, localOffset);
                            }
                            break;
                        }           
                    case "+":
                        {
                            int newOffset = _match((Production)item, localOffset);
                            // make sure first case matches
                            if (newOffset == -1)
                                return -1;
                            while (newOffset != -1)
                            {
                                localOffset = newOffset;
                                newOffset = _match((Production)item, localOffset);
                            }
                            break;
                        }
                    case "OPTIONAL":
                        {
                            int newOffset = _match((Production)item, localOffset);
                            if (newOffset != -1)
                                localOffset = newOffset;
                            break;
                        }
                    case "GROUP":
                        localOffset = _match((Production)item, localOffset);
                        if (localOffset == -1)
                            goto CLEANUP;
                        break;
                    case "ALTERNATOR":
                        localOffset = _matchAll(((Production)item).Content, localOffset);
                        if (localOffset == -1)
                            goto CLEANUP;
                        break;
                }
            }

            return localOffset;

            // my one cardinal sin
            // clean up the current ASTNode content
            CLEANUP:
            int sizeDiff = _semanticStack.Last().Content.Count - cachedSize;
            if (sizeDiff > 0)
            {
                if (sizeDiff == 1)
                    _semanticStack.Last().Content.RemoveLast();
                else if (cachedSize == 0)
                    _semanticStack.Last().Content.Clear();
                else
                    _semanticStack.Last().Content.RemoveRange(cachedSize - 1, sizeDiff);
            }

            return -1;
        }

        private int _matchAll(List<IGrammatical> productions, int offset)
        {
            int localOffset;
            foreach (Production production in productions)
            {
                localOffset = _match(production, offset);

                if (localOffset != -1)
                    return localOffset;
            }
            return -1;
        }
    }
}
