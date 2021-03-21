## Syntax Update

- no more semicolons and braces
  - use `do`, `of`, and `to` as beginnings of blocks
  - use newlines and indentations
  - semicolons sometimes used in compound statements
  - cleans up code
- new commenting style:
  - `#` for line comments
  - `#!` for multiline comments (closing `!#`)
  - allows us to use `//` as the floor division operator instead of hideous `~/`
- use `**` as power operator instead of `~^` (ich...)
- change annotations to use `@` instead of `#`
- use single arrow (`->`) instead of double arrow (`=>`)
  - former looks nicer and is easier to type
- the `do` can be elided after "lonely" control flow keywords.
  - eg. `else` instead of `else do` (redundant)
  - general rule: if the control flow keyword contains no
  content -> `do` can be elided
- whitespace *aware*
  - spaces are only used to delimit tokens (purely lexical)
  - parser will expect newlines, indents, and dedents where
  specified (if they are not there, it will error)
  - it will allow newlines anywhere in the code
    - even if the newline is unexpected
    - can cause parser to misinterpret (predictably)
    - eg. `x = y\n+ 2` will cause an error (read as two separate statements)
  - it will **balanced** indentation when unexpected
    - for every unexpected indent, there must by an equivalent dedent
    before the next indent or dedent token will be accepted
    - it will prioritize balancing indentation
    - can lead to errors if an indent is expected, but there is
    an unbalanced indent (no dedent) -> indent will marked as erroneous
  - all other forms of whitespace (carriage returns, etc.) will be ignored
- the `\` character can be used as *split-join* character to mark the
next line as a continuation of the first.  This will void all indentation
checking on the next line (works like it does in Python).
  - compiler should still provide error messages corresponding to the
  correct line: only joined from a lexical-semantics perspective
- `this` is simply a special identifier
- wrap match content in parentheses
- ignore indentation between (), {}, and []
- also ignore indentation in block definitions
  - for, if, elif, match, async-for, with
  - inside with expressions
- reorder match expression (`match ... to` instead of `... match to`)
- combine multiple annotations into single line
  - `@[inline, inplace]`
- revised iterator syntax
  - use `in` instead of `<-` (easier to type and looks better)
  - use `for` instead of `|` in comprehensions (looks better, removed ambiguity)
  - "Python style"
  