# TODO

- add package linker
- add 2 sanitizers (constexpr and memory) [Note: constexpr does in depth checking on all constant expressions]
- remove MirrorType and declare builtins in SymbolTable as necessary (via prelude)
- add prelude (later)
- clear up the behavior of null
- add pattern matching on variables after `is`
  * `x is t: Type`
- add special binding syntax to allow for binding onto all types of pointers,
arrays, lists, and dictionaries
  * `for *` - pointers
  * `for {}` - arrays
  * `for []` - lists
  * `for {:}` - dictionaries
  * all of them use `T` as their generic placeholder
- make sure finalizers work as intended
- add annotations to describe memory and program behavior (using `#` syntax)
  * File Level Annotations: `#unsafe` or `#res_name "test"`: effect file behavior generally
  * Function Annotation: `#intrinsic` or `#extern`: to effect how function compiles
  * Struct Annotation: `#joined`: effect how structs compile and initialize
- add generic function groups
- fix *void casting
- add context-based inferencing for lambdas in `case` and `if` expressions
- add `#impl` to create structs for intrinsic types like strings or arrays
  * `#impl "str"`
- add `super(Parent)` syntax to get overidden methods of interface
- add `raise x, y` to raise expression locals to upper scope
- change context manager keyword to `with` instead of `from`
- add asynchronous lambdas
  * looks like: `async |x| => x`

# TESTING

- static get
- change include syntax to use `::` instead of `.`
  * include { Println, Scan } from io::std;
  * include ..a::b;

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
- sanitizers should be embedded in visitor
- make sure to compile `this` as hidden pointer
  * `&this` does nothing at a generated code level
  * `this.property` compiles to `this->property`
  * whenever `this` is used as a value type (non-reference) it is implicitly
    dereferenced

# THOUGHTS

- add string interpolation
  * $"Hello, my name is: {name}."
- add multiline strings
  * """hi my name jeff"""
  * should they compile with newlines?

