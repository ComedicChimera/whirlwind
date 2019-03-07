Changes:
- unsized arrays
- removal of classes
- interfaces can now be bound to any type and classify any type
  * `interf for` syntax
  * `interf ... is inherits` syntax
- removal of enums
- all high level type constructs have a short hand generic syntax
  * interf<T> for
  * interf name<T>
  * type name<T>
  * struct name<T>
- introduction of high level type classes
  * type Integer int;
  * type Positive int(v) when v > 0;
  * type Color | Red | Blue | Green;
  * type Option<T> | Some(T) | None;
  * type FloatOrString<T> | Float(T) when T is float | String(T) when T is str;
- monadic typing
  * adding `>>=`, `>=>`, and `:>` operators
  * adding Monad<T> interface
- improved operator overloading syntax
- `then` operator
  * allow value to be accessed through `$0` syntax if one exists
- changing conditional operator
  * `cond ? case1 : case2` now is `case1 if cond else case2`
- constancy now bound on type not on variable
  * `let x: const T;` is equivalent to `const x: T;`
  * `x: const T` in function call is equivalent to `const x: T`
- expression local variable declarations
  * `name = t`
- variance added to interfaces
- `~*` function composition operator
- introduction of struct constructors
- add type specific compositions
  * { comp } for arrays
  * [ comp ] for lists
  * { dict_comp } for dictionaries
- add function overloading
- remove privacy
