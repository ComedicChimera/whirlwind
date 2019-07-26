# TODO

- add package linker
- add 2 sanitizers (constexpr and memory) [Note: constexpr does in depth checking on all constant expressions]
- remove MirrorType and declare builtins in SymbolTable as necessary (via prelude)
- add prelude (later)
- clear up the behavior of null
- make sure type classes that have aliases can apply the `as` operator to unpack
them
and make sure that unpacking is functional in its more complex state (as in the
following works as intended)
  * from docs: `let num = Number::Int(3); let t: int = from (num as Int);`
- add pattern matching on variables after `is` and 'as'
  * `x is Type t`
  * `x as Type t`
and make sure constancy works
  * see docs on Type Classes
- fix references to have a more logical behavior
- add special binding syntax to allow for binding onto all types of pointers
- make sure finalizers work as intended
- allow for total import of package
  * include { ... } from package;
- fix decorators to be up to snuff with the docs
  * be able to take arguments with only one level of wrapping
  * be able to omit `[]` for single decorator applications
  * be able to work with overloading functions
  * be able to apply to decorators from other packages (static get)
  * see all in Chapter 16 of Whirlwind docs
- add annotations to describe memory and program behavior (using `#` syntax)
  * File Level Annotations: `#unsafe` or `#res_name "test"`: effect file behavior generally
  * Function Annotation: `#intrinsic` or `#extern:` to effect how function compiles
  * Struct Annotation: `#joined`: effect how structs compile and initialize

# THOUGHTS

- add positioning data to some or all of the action tree (maybe identifiers?)
- make sure to properly name arg and parameter variables
- add string interpolation
- find a way to vary behavior based on type for interfaces (interface level method variance, a buffed is operator, etc.)
  * although it is possible to do this via overloading
- rework casting syntax to be more friendly

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

# TESTING

- static get
- change include syntax to use `::` instead of `.`
  * include { Println, Scan } from io::std;
  * include ..a::b;

