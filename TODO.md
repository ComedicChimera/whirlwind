# TODO

- change self referential types to work off a prefixed name system and remove self-type
- add package linker
- add 2 sanitizers (constexpr and memory) [Note: constexpr does in depth checking on all constant expressions]
- remove MirrorType and declare builtins in SymbolTable as necessary
- add prelude (later)
- add support for generic binding
- allow for context-based inferencing (for lambdas)
- rename closure to lambda (closure is a specific type of lambda and although your
lambdas can act like closures, they aren't always technically)
- clear up the behavior of null

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
- change include syntax to use `::` instead of `.`
  * include { Println, Scan } from io::std;
  * include ..a::b;

