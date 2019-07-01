# TODO

- change self referential types to work off a prefixed name system and remove self-type
- add package linker
- add 2 sanitizers (constexpr and memory) [Note: constexpr does in depth checking on all constant expressions]
- remove MirrorType and declare builtins in SymbolTable as necessary
- add prelude (later)
- add support for generic binding
- allow for context-based inferencing (for lambdas and enumerated type classes)
- clear up the behavior of null
- make overloading based on parameter difference instead of coercion
- update type class to use guards on the type constructor as opposed to having
a separate (and illogical) value restrictor
  * type Type Val(v: Int) when v < 3;
- make sure type classes can only allow one alias (for logic purposes)
and make sure that unpacking is functional in its more complex state (as in the
following works as intended)
  * from docs: `let num = Number::Int(3); let t: int = from (num as Int);`
- allow decorators to work with function groups
- toughen up constancy a bit (allow it be casted away, but make it so that a const can't be made non-const by coercion)

# THOUGHTS

- add positioning data to some or all of the action tree (maybe identifiers?)
- make sure to properly name arg and parameter variables
- change integer literals to default to signed integers and make signed-unsigned coercion easier
- add annotations to describe memory and program behavior (using `#` syntax)
- add string interpolation
- find a way to vary behavior based on type for interfaces (interface level method variance, a buffed is operator, etc.)
  * although it is possible to do this via overloading

# FUTURE

- add operator overloading during generation

# TESTING

- static get
- change include syntax to use `::` instead of `.`
  * include { Println, Scan } from io::std;
  * include ..a::b;

