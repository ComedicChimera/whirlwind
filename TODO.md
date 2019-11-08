# PLAN

- Build the MVP sans-runtime (everything you can without any runtime/OS support)
  * all basic instructions, intrinsics, constructs, interface (etc)
- Build DLL Loader (to bind to OS) and all the stuff needed for that
- Implement Abstract Concurrency Interface (concurrency without any actual behavior)
- Implement Dynamic Memory (Heap Allocator)
- Implement Core Concurrency (Fibers, Futures, Mutex, etc.)
- Implement Any Remaining Elements of Core Library
- Implement Most Basic Packages
  * `io`, `math`, `encoding`, `strconv`, `os`, `time`, `random`
  * implement `math` in pieces (do the small, simple stuff first, then the big stuff)
  * same for `random` (no need to do anything ridiculously complicated until later)
- Implement Remaining "Non-Essential" Packages
  * see `std-lib.txt`

# TODO

- get yo naming right in documentation
  * "Value-Enumerated Type Class" -> "Algebraic Type Class"
  * "function objectification" -> First Class Functions
  * flip "deductive" and "inductive" type inference (it makes more sense the other way round)
  * "case expression" -> "select expression"
- get strings working
  * test all of the conversion functions
  * make sure verify char works for unicode input (registers as 2 chars instead of 1)
  * make sure chars compile correctly
- mangle everything, reference everything via custom symbol "table"
  * come up with way to handle overloads (LLVMSymbol class?)
- get structs up and running
  * variable initializers
- make type impls actually work
- finish functions
  * deal with multifile overloading
  * any other problems that could come up
- add all the types
- WORK ON GENERATOR

# TESTING

- ALL DE NEW STUFF
- PACKAGE SYSTEM

# FUTURE

- BE CONSCIOUS ABOUT EVERY TRANSLATION: NOTHING IS EVER "THAT" SIMPLE
  * how can this optimized?
  * how can this be condensed?
  * could this cause problems?
- think about temporary objects and null initialization and how it can be optimized (+ optimization for all std lib constructs)
- make sure Whirlwind inserts `delete` whenever possible, but no "plausible" deletes, slows down too much
  * if compiler isn't sure, it does nothing
  * add note about this in memory section of guide
- add operator overloading during generation
- add strict group overload matching during compilation
- ensure compiled code handles coercion properly (particularly on tuples)
- closures must obey the following behavior
  * each closure creates a dynamically allocated copy of its state (that it stores between calls)
  * its state is not copied between calls or when the closure itself is copied
  * closure's state must be carefully watched
- ensure null initialization is pervasive
  * this should work: `func f() int => x; let x = f();`
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
  * `this as Type` implicitly copies this whereas `this as *Type` does not
- remove MirrorType and declare builtins in SymbolTable as necessary (via prelude)
- make sure then functions as a NON-CONDITIONAL chaining operator
- `:>` functions as conditional monadic expression bind operator
  * if bind succeeds, execute the expression with extracted value in `value`
  * right-hand expression always returns something coercible to the source type
  * if the bind fails, return the value of the bind operator
- make sure to process generic binding appropriately
- when generating code, make sure to add in deletes for dynamically allocated memory
- make sure to check and apply externals and intrinsics where necessary
- make sure package linker works on generative side
- add prelude 
- remember, ranges can go in both directions are are inclusive on both sides
- make sure to inline expr functions
- make sure naming conventions are properly handled (ie. should std-lib have prefix lib::std?)
- make sure includes bring in all of the necessary information (ie. if you import something, you import all of it)
- lists and dicts must cleanup after themselves
  * both are dynamically allocated and need to handle that appropriately
- add implementations for intrinsics

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
- add some higher level pattern matching capability
  * more than just tuples and type classes
  * `[x, 4, _, ...]`
  * `struct {y: t}`
- separate bitwise and logical operators?
- add support for type aliases in pattern matching?
- add different types of type restrictors (strict and lax)
