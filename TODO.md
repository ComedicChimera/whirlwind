# TODO

- remove template syntax (change to pure generic syntax)
  * interf<T> for
  * interf name<T>
  * type name<T>
  * struct name<T>
  * func name<T>() 
  * async name<T>()
- change from using "templates" to using "generics"
- add Monad<T> interface
- add constancy propagation
- add generic operator overloads (*)
- add prelude (later)
- add 3 sanitizers (const, constexpr, and memory) [Note: constexpr does in depth checking on all constant expressions]
- remove MirrorType and declare builtins in SymbolTable as necessary
- remove agents
- remove exceptions
- add struct initializers
- add '!' tag to data types (means it is automatically deallocated with scope close)
- add range syntax
- add annotations to describe memory and program behavior (using `#` syntax)
- change decorator syntax to `@` instead of `#`
- make sure type classes coerce properly
- allow for static data types (that persistently share data)
- ability to add capture to both function and to block

# THOUGHTS

- add positioning data to some or all of the action tree (maybe identifiers?)
- allow interfaces and agents to have variance
- agent id methods
- make sure to properly name arg and parameter videos

# FUTURE

- add operator overloading during generation

# COMPLETED

- remove null coalescion operator
- changing conditional operator
  * `cond ? case1 : case2` now is `case1 if cond else case2`
- unsized arraysw
- removal of enums
- removal of classes
- introduction of high level type classes
  * type Integer int;
  * type Positive int{v} when v > 0;
  * type Color | Red | Blue | Green;
  * type Option<T> | Some(T) | None;
  * type FloatOrString<T> | Float(float) | String(str);
- introduction of struct constructors
- add type specific comprehensions
  * { comp } for arrays
  * [ comp ] for lists
  * { dict_comp } for dictionaries
- interfaces can now be bound to any type and classify any type
  * `interf for` syntax
  * `interf ... is inherits` syntax
- `~*` function composition operator
- `then` operator
  * allow value to be accessed through `val` syntax if one exists
- expression local variable declarations
  * `name = t`
- common interface coercion (implements on interface type)
- change list and array coercion
  * list to unsized array
  * array to list
- after clause (except clause)
- static get
- reference types
- new syntax
- agents
- object level method variance
- recursive type definition
- is operator
- exception ending
- add `>>=` and `:> operators
- prevent declaration of null type lists
- add empty type classes
  * type Thing;
- improved operator overloading syntax
- allow compound assignment with any operator
- change include syntax to use `::` instead of `.`
  * include { Println, Scan } from io::std;
  * include ..a::b;
- make interface static on non-user types
- add function overloading
- add monadic value extraction
  * from T
