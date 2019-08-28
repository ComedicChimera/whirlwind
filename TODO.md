# TODO

- fix type system to be more usable (for simple types)
  * unsigned integers: `u8, u16, u32, u64`
  * integers: `i8, i16, i32, i64`
  * floats: `f32, f64`
  * keep `str` the same
  * allow `char, byte` to cast to `u16, u8` respectively
  * define `int, uint, voidptr` via runtime core for use when necessary
  * sizes coerce upward and cast downward, you must cast between signed and unsigned
  * integer literals default to whatever `int` is, floats default `f32`
- update ranges to go in both directions and have inclusive bounds on both sides
  * 1..10 is actually `1, 2, 3, ... , 9, 10`
  * 10..1 generates like `10, 9, ... , 3, 2, 1`

# TESTING

- ALL DE NEW STUFF

# FUTURE

- add operator overloading during generation
- add strict group overload matching during compilation
- ensure compiled code handles coercion properly (particularly on tuples)
- closures must obey the behavior described in the docs
  * they share their state, don't copy it
- ensure null initialization is pervasive
  * this should work: `let x = f(); func f() int => x;`
- account for out of order variable declaration if necessary
- distinguish between fibers, threads, and processes.
  * fiber: lightweight, non-OS, concurrent executor
  * thread: heavier, OS-based, concurrent executor
  * process: heavy, OS-based, concurrent, non-Whirlwind owned executor
- when implementing package linker, make sure to give prefix to visitor
- during compilation, make sure to acknowledge the effects of captures
- make sure to compile `this` as hidden pointer
  * `&this` does nothing at a generated code level
  * `this.property` compiles to `this->property`
  * whenever `this` is used as a value type (non-reference) it is implicitly
    dereferenced
- remove MirrorType and declare builtins in SymbolTable as necessary (via prelude)
- make sure then functions as a CONDITIONAL CHAINING operator
  * if the previous expression is a boolean, continue only if true
  * if the previous expression is not a boolean, continue if there is no error
  * if the chain is incomplete, it simply returns the null value of the last type
- make sure to process generic binding appropriately
- when generating code, make sure to add in deletes for dynamically allocated memory
- make sure to check and apply externals and intrinsics where necessary
- make sure package linker works on generative side
- add prelude 
- make sure `#impl` is used in generating intrinsics (and in visiting type interfaces
for those intrinsics under certain conditions?)

# THOUGHTS

- add string interpolation
  * $"Hello, my name is: {name}."
- add multiline strings
  * """hi my name jeff"""
  * should they compile with newlines?
- consider adding privacy as something more tangible than just convention
  * add a `priv` modifier
- make the overloading for generic function groups account for restrictors
- add context-based inferencing for lambdas in `case` and `if` expressions
- allow for type decomposition (infer one type from another as in generics)
  * like `func toDict<T to (K, V)>() [K: V]`
  * or maybe `func toDict<T as (K, V)>() [K: V]`
  * or even without generics `func toDict() T to [K: V]` where to compiler infers the missing types
  

