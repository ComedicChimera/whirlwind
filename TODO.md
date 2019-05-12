# TODO

- change self referential types to work off a prefixed name system and remove self-type
- add package linker
- add 2 sanitizers (constexpr and memory) [Note: constexpr does in depth checking on all constant expressions]
- remove MirrorType and declare builtins in SymbolTable as necessary
- add prelude (later)

# THOUGHTS

- add positioning data to some or all of the action tree (maybe identifiers?)
- make sure to properly name arg and parameter variables
- change integer literals to default to signed integers and make signed-unsigned coercion easier
- add annotations to describe memory and program behavior (using `#` syntax)
- add string interpolation

# FUTURE

- add operator overloading during generation

# TESTING

- static get
- object level method variance
- change include syntax to use `::` instead of `.`
  * include { Println, Scan } from io::std;
  * include ..a::b;
- add support for generic binding
