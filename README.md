# Whirlwind

The New Home of the Formerly Known as SyClone Language

## Grammatical Notes

We use a grammatical syntax that roughly follows **EBNF** standard, however, there have been some slight modifications made:

- No token literals; they are stored in a separate file.
- No `?` operator; just use the `[]` syntax
- Two kinds of comments
  - The Singleline `// comment`
  - The Multiline `/* comment */`
- All tokens must be incased in single quotes.
  
Our new parser is compatable with left recursive and partially ambiguous grammars.  It parses using an approach similar to that of an Earley Parser; it allows for multiple possible solutions.

## Action Trees

Whirlwind uses a system of Action Trees to represent its IR.
There are two different variations of Action Trees:
**raw** and **reduced**.  The raw action tree is generated
directly from the code during semantic analysis. They are
effectively typed ASTs.  However, the reduced tree is
the deabstracted and optimized IR of Whirlwind.  There
are a lot fewer nodes in the reduced tree, but the nodes
contain significantly more information.

Each of these trees has a reference for all of the tree variations
including an explanation of what each node is and the "parameters"
it takes.

## Tree References

- [Raw](https://github.com/ComedicChimera/Whirlwind/blob/master/docs/raw_trees.md)

- [Reduced](https://github.com/ComedicChimera/Whirlwind/blob/master/docs/reduced_trees.md)

It can be helpful to think of these trees as somewhat similar to
S-Expressions. However, they are strongly typed, type annotated and thoroughly
checked.
