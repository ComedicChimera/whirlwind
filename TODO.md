# TODO

- add predefinitions (#pred)
  * make sure they assemble correctly
- rename Type Classes to Custom Types
  * completely (as in errors, documentation, grammar, etc.)
- add `own` modifier
  * tells compiler which block can automatically deallocate heap memory
  * in structs and on arguments
- demote struct and arg initializers (this is important, make it work)
- get structs up and running
  * variable initializers
- finish functions
  * deal with multifile overloading
  * any other problems that could come up
- add all the types
- WORK ON GENERATOR

# TESTING

- ALL DE NEW STUFF
- PACKAGE SYSTEM
- INFERRED TYPES ON CASE EXPRESSIONS
- HIGH LEVEL INITIALIZERS (not demoted)

# FUTURE

- BE CONCIOUS ABOUT EVERY TRANSLATION: NOTHING IS EVER "THAT" SIMPLE
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
- closures must obey the behavior described in the docs
  * they share their state, don't copy it
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
- make sure then functions as a CONDITIONAL CHAINING operator
  * if the previous expression is a boolean, continue only if true
  * if the previous expression is not a boolean, continue if there is no error
  * if the chain is incomplete, it simply returns the null value of the last type
- make sure to process generic binding appropriately
- when generating code, make sure to add in deletes for dynamically allocated memory
- make sure to check and apply externals and intrinsics where necessary
- make sure package linker works on generative side
- add prelude 
- remember, ranges can go in both directions are are inclusive on both sides
- make sure to inline expr functions
- make sure naming conventions are properly handled (ie. should std-lib have prefix lib::std?)

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
- add literal suffixes for different types:
  * `0.0f` - float
  * `0l` - long
  * `0u` - uint
- add some higher level pattern matching capability
  * more than just tuples and type classes
  * `[let x, 4, _, ...]`
  * `struct {y: let t}`
- separate bitwise and logical operators?

