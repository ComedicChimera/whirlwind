# Compiler Info
This file contains information about the compiler to help enable to contribute the repository and as a reference
for all who are currently working on the compiler.

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

An action tree represents a typed AST, more structured AST.
They signify operations, literals and, all other values are a lot
easier to process.  Furthermore, all action trees are pre-type-checked
meaning nothing on them needs to be proofed during later phases of the compiler.

- [Tree Reference](https://github.com/ComedicChimera/Whirlwind/blob/master/Whirlwind/docs/trees.md)

It can be helpful to think of these trees as somewhat similar to
S-Expressions. However, they are strongly typed, type annotated and thoroughly
checked.

***NOTE*** *The _ character should never be declared, merely ignored*.

## Type Conversions

This document describes how types are converted from Whirlwind to LLVM.

- [Type Conversions](https://github.com/ComedicChimera/Whirlwind/blob/master/Whirlwind/docs/type_conversions.md)

This document has nothing to do with casting: that is in the Language Reference.
