# TODO

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

- introduction of high level type classes
  * type Integer int;
  * type Positive int{v} when v > 0;
  * type Color | Red | Blue | Green;
  * type Option<T> | Some(T) | None;
  * type FloatOrString<T> | Float(float) | String(str);
- interfaces can now be bound to any type and classify any type
  * `interf for` syntax
  * `interf ... is inherits` syntax
- common interface coercion (implements on interface type)
- static get
- object level method variance
- add `>>=` and `:> operators
- add empty type classes
  * type Thing;
- improved operator overloading syntax
- change include syntax to use `::` instead of `.`
  * include { Println, Scan } from io::std;
  * include ..a::b;
- add monadic value extraction
  * from T
- ability to add capture to both function and to block
  * with[]
  * with[a, b, c]
  * with[const e, own f, val g]
  * with[!z]
- change from using "templates" to using "generics"
- remove template syntax (change to pure generic syntax)
  * interf<T> for
  * interf name<T>
  * type name<T>
  * struct name<T>
  * func name<T>() 
  * async name<T>()
- add support for generic binding
