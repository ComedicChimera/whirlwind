# Considerations

This file contains proposed features for Whirlwind (so I don't forget them).  These
are things that I am not sure I want to in the language or want to change about the
language but that I do want to remember for later.

- fn(...list) for passing sequences as indefinite arguments
  * Whirlwind implements indefinite arguments as "arrays" internally anyway
- Internal/private fields and methods
  * By definition: using `priv` keyword or similar
- Dependent methods (methods that only appear on certain generates of an interface: eg. `sum` or `to_dict`)
- Separate syntax for creating a new value on the heap and referencing an old value
  * `make` for R-Values and new types
  * `&` for L-Values; possibly add some form of lifetime checking
  * `[]` and `{}` for empty arrays, lists, and dicts (use type inference to figure out meaning)
- In-expression type labelling: `:: int` (Haskell style) -- help the inferencer out
  * Special case: `:: <T>` infers type parameters
- Use one operator for named accessing: `.`
  * `import math.complex`
  * `rand.randint(2, 3)`
  * `Option::<int>.None`
- Add `?` operator for monadic accumulation in expressions
  * `sqrt(x)? + 2` yields value `Some(sqrt(x) + 2)` if `x >= 0` and `None` if `x < 0`
  * OR: put the `?` operator on the outside: `(sqrt(x) + 2)?` to have the same effect
    - should it have parentheses in this case?

