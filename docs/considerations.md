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
- Change to LLVM-style naming conventions for numeric types
  * `u8`, `u16`, `u32`, `u64` for unsigned integers
  * `i8`, `i16`, `i32`, `i64` for signed integers
  * `f32`, `f64` for floating point types
  * `int` and `uint` are aliases for integer types
  * `byte` is an alias for `u8`
  * `rune` is an alias for `i32`
