using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using System.Text.RegularExpressions;
using System.Linq;

namespace Whirlwind.Parser
{
    struct Token
    {
        public string Type;
        public string Value;
        public int Index;

        public Token(string type, string value, int ndx)
        {
            Type = type;
            Value = value;
            Index = ndx;
        }

    }

    class Scanner
    {
        private Dictionary<string, string> _tokenSet;

        public Scanner(string tokenPath)
        {
            string json = File.ReadAllText(tokenPath);
            _tokenSet = JsonConvert.DeserializeObject<Dictionary<string, string>>(json);
        }

        public List<Token> Scan(string text)
        {
            text = _removeComments(text);

            var tokens = new List<Token>();
            foreach (var item in _tokenSet.Keys)
            {
                var regex = new Regex(_tokenSet[item]);

                foreach (Match match in regex.Matches(text))
                {
                    tokens.Add(new Token(item, match.Value, match.Index));
                }

                text = regex.Replace(text, _replaceRepeater);
            }

            tokens.Sort(delegate (Token a, Token b)
            {
                if (a.Index == b.Index) return 0;
                else if (a.Index > b.Index) return 1;
                else return -1;
            });


            return tokens;
        }


        private string _removeComments(string text)
        {
            var singlelineRegex = new Regex("//.*\n*");
            var multilineRegex = new Regex(@"/\*(?:(?!\*/).)*\*/", RegexOptions.Singleline);

            text = singlelineRegex.Replace(text, _replaceRepeater);
            return multilineRegex.Replace(text, _replaceRepeater);
        }

        private string _replaceRepeater(Match match)
        {
            return String.Concat(Enumerable.Repeat(" ", match.Value.Length));
        }
    }
}
