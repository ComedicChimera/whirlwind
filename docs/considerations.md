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
  
