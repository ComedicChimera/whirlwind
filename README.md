# Whirlwind
The New Home of the Formerly Known as SyClone Language

## Grammatical Notes
We use a grammatical syntax that roughly follows **EBNF** standard, however, there have been some slight modifications made:

  - No token literals; they are stored in a separate file.
  - No `?` operator; just use the `[]` syntax
  - Two kinds of comments
    * The Singleline `// comment`
    * The Multiline `/* comment */`
  - All tokens must be incased in single quotes.
  
Our new parser is compatable with left recursive and partially ambiguous grammars.  It parses using an approach similar to that of an Earley Parser; it allows for multiple possible solutions.

