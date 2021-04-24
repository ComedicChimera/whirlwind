# Considerations

This file contains proposed features for Whirlwind (so I don't forget them).  These
are things that I am not sure I want to in the language or want to change about the
language but that I do want to remember for later.

- fn(...list) for passing sequences as indefinite arguments
  * Whirlwind implements indefinite arguments as "arrays" internally anyway
- Internal/private fields and methods
  * By definition: using `priv` keyword or similar
- Dependent methods (methods that only appear on certain generates of an interface: eg. `sum` or `to_dict`)
- In-expression type labelling: `:: int` (Haskell style) -- help the inferencer out
  * Special case: `:: <T>` infers type parameters
- Use one operator for named accessing: `.`
  * `import math.complex`
  * `rand.randint(2, 3)`
  * `Option::<int>.None`
- Add `?` operator for monadic accumulation in expressions
  * `sqrt(x)? + 2` yields value `Some(sqrt(x) + 2)` if `x >= 0` and `None` if `x < 0`
