# Changes

List of Adjustments from Previous Version

## Syntax Update

- no more semicolons and braces
- use `do`, `of`, and `to` as beginnings of blocks
- use newlines and indentations
- semicolons sometimes used in compound statements
- eg. c-style for loops
- cleans up code
- allow for argument parentheses to be elided if
the function takes no arguments
  - eg. `func main do`
- the `do` can be elided after "lonely" control flow keywords.
  - eg. `else` instead of `else do` (redundant)
  - eg. `loop` instead of `loop do` (again, redundant)
  - general rule: if the control flow keyword contains no
  content -> `do` can be elided
- whitespace *aware*
  - spaces are only used to delimit tokens (purely lexical)
  - parser will expect newlines, indents, and dedents where
  specified (if they are not there, it will error)
  - it will allow newlines anywhere in the code
    - even if the newline is unexpected
    - can cause parser to misinterpret (predictably)
    - eg. `x = y\n+ 2` will cause an error (read as two separate statements)
  - it will **balanced** indentation when unexpected
    - for every unexpected indent, there must by an equivalent dedent
    before the next indent or dedent token will be accepted
    - it will prioritize balancing indentation
    - can lead to errors if an indent is expected, but there is
    an unbalanced indent (no dedent) -> indent will marked as erroneous
  - all other forms of whitespace (carriage returns, etc.) will be ignored
- the `\` character can be used as *split-join* character to mark the
next line as a continuation of the first.  This will void all indentation
checking on the next line (works like it does in Python).
  - compiler should still provide error messages corresponding to the
  correct line: only joined from a lexical-semantics perspective
- `this` is simply a special identifier
- wrap match content in parentheses
- ignore indentation between (), {}, and []
- also ignore indentation in block definitions
  - for, if, elif, match, async-for, with
  - inside with expressions
- use `:=` declaration operator for `with` and c-style for loop
- reorder match expression (`match ... to` instead of `... match to`)

## Removals

- `->` operator, just use `.` for everything
- constructors (use init-lists)
- group overloading
- old `match` statement (ie. `match<...> expr to ...`)
- `select` keyword => replaced by match
- pattern matching on `is`
  - moved into `match` logic
- `from ... as` syntax
  - just use pattern matching
- special methods: not necessary anymore
  - hide functionality
- expression local bindings
  - no expression local variables or expression local chaining
  - all symbols declared in expressions are declared in enclosing
  scope
  - cleaner, move obvious, less bad code
- old partial function syntax
  - `|f ...)` just looks noisy
  - still a feature, done much more logically
    - eg. `f(_, 2)`
    - see **Syntactic Sugar and Smaller Adjustments** for details
- the `default` keyword
  - `case _ do` is just as clear
  - inline with rest of language (inc. inline `match`)
- C-Style for loop
  - accomplishable (basically) with iterators and while loops
  - just looks ugly
- implicit infinite loop
  - just kinda looks ugly
  - not necessarily unclear but really not very idiomatic

## Control Flow/Conditionals Update

- two kinds of loops
  - `while` is for conditional loops
  - `for` is for iterator-based loops
- the `match` upgrade (and condensing)
  - all use `to` to begin their blocks
  - match statement
    - works like old select statement
    - allow for `when` conditions
    - made for values
    - allow for `fallthrough`
      - continue to next case
      - `fallthrough to match`
        - falls through to next matching case
    - uses `case` and `default`
      - both use `do` blocks
    - full pattern matching
    - written as `match expr to`
  - match type statement
    - used to match over multiple possible
    types
    - no pattern matching (not necessary)
    - same mechanics as regular match statement
    but it compares types instead of values
    - written as `match expr type to`
    - `type` is keyword!
  - match expression
    - works like old select expression
    - full pattern matching
    - exhaustive
    - suffix: `match to`
    - requires block indentation
    - no commas, use newlines (whitespace sensitive)
  - match type expression
    - similar mechanics to match expression
    but for comparing types inline
    - no pattern matching
      - except for `_ =>` for default case
    - suffix: `match type to`
  - match operator
    - used to implement single value pattern matching
      - replacement for "case expression"
    - replaces pattern matching behavior of `is`
      - eg. `expr match Some(v)`
    - no block, distinguishing factor, also suffix

## Memory Update (Ownership, Nullability, Constancy, Category)

- Ownership
  - use references instead of pointers
    - like a pointer except they are not treated as memory addresses
    - no pointer arithmetic (no direct address manipulation)
      - can be supplemented (unsafely) via intrinsics
    - cleaner syntax
  - two kinds of references: regular and const references
  - `make` returns a reference
    - if it is a single item, it returns a reference to that item
    - if it is a block, returns a reference to an array
    - can be resized via `make ref to new_size`
      - called resize statement
      - new_size has same semantics as regular make
  - created with syntax `&value` or `&const value`
  - type label: `&type` or `&const type`
  - dereferences must be explicit; however,
    - many operators have reference forms (as well as nullable forms)
    - eg. `ref_struct.x`, `ref_array[2]`
  - compiler-determined lifetimes, user-defined ownership status
    - ownership informs lifetime
  - protective semantics
    - lifetime congruence (eg. can't return a stack reference, can't make a stack reference global)
    - `own` is a type specifier that determines what type of reference we are dealing with
    - ownership status must match exactly
      - maintained when value categories differ
      - dropped when they are the same
    - operator usage: `:>`, `delete`, and resize on mutable owned references, `=` on unowned references
    - **note**: the move operator can be elided to an assignment operator if the compiler can determine
    that the reference stores no meaningful value
      - via nullability checks / open-form initialization / explicit `null`
  - lifetimes can be bound to data structures (selectively, via. `own` keyword)
    - ownership => lifetime therefore lifetime bound to owner
  - double references are not allowed (and of course triple, quadruple, etc. are also disallowed)
    - can use combo of `own` or `const` to achieve equivalent behavior to C++ (rvalue references)
- Nullability
  - references have a nullability status
  - designated like so `&byte?`
  - non-nullable references cannot be deleted (implicitly or explicitly)
  - nullable operators only valid on nullable references
  - type logic:
    - cannot be casted or coerced (given or taken away) on lvalue or cvalue references
    - rvalue references can become nullable but cannot become non-nullable
    - null coalescence (and possible null checking if exprs) can cause it to disappear.
  - null safety verification
  - nullable operators must be used on nullable types unless compiler determines that
  you have already checked nullability
    - combines with ownership model
  - in place of several nullable operators, use single null-test operator `?`
  - added as a trailer suffix (`?`)
  - combines with the nearest operation if possible
  - denotes that if the type is null (or invalid), will accumulate to null
  - if not, it simply accumulates the value to null if it is invalid
    - eg. `ref? == null` would be a way to test if a reference is invalid
  - makes dereference syntax a bit nicer `*ref?`
  - solves reference validity vs. reference nullability
- Constancy
  - only applies to references and variables (mutable, named values)
  - for variable declarators:
    - `const x ...`
    - constancy will be applied as an optimization after semantic checking
      where possible
  - same syntax for structs and function arguments
  - for references:
    - `&const x` (create a const reference to x)
    - `&const type` (const reference of type)
  - reference constancy is viral
    - `let x = &const y` (x is now a const reference)
    - x can still be mutated, the reference cannot be
  - value constancy is not
    - `const x = 10; let y = x` (y is not constant)
    - (semicolons for demonstration purposes)
  - casting rules: mutable -> constant, constant -/> mutable
  - methods can be constant (explicitly - via `const` keyword)
    - they can also be inferred to be constant
  - cannot take a non-const reference to a constant value
- Value Categories
  - lvalue (well-defined, mutable value, able to take both kinds of references to it)
  - rvalue (undefined, immutable value, unable to take any kind of reference to it)

## Vectors

- vector constructor: `<value : const_len>` or `<{elems...}>`
- vector data type: `<size>type` (only valid on integral, floating point or pointer types)
- vector generics: `<T, N>` (N is a size parameter to a vector data type)
- all basic arithmetic operations are valid on vectors (scalar and vector mult)
- additional intrinsics and utilities (eg. `__vec_sum(v)` and `__shuffle_vec(v1, v2, mask)`)
- `#vec_unroll` annotation to cause vector functions to be optimized (as much as possible)
- extended (later) as part of math library (intended for general purpose use, also used in matrices and complex numbers)
- vector array initializers should be compiled as `shufflevector` if possible
- VECTORS ARE ITERABLE

## Context Managers

- Allow for the creation of managed/safe contexts
- Uses the `Contextual` interface
- Inspired by Monads and `using` in C#
- Syntax: `with item <- f() do`
  - Also has an expression form: replace `do` with `=>` followed by an expression
  - Can include multiple items (separated by semilinebrs)
  - Can perform unpacking inline
  - Usage of `_` allowed (though not common)
- Has a `finally` clause that is guaranteed to run after (even in context of a return)
  - Perhaps even on a runtime panic?
    - Need to find some way to allow `Contextual` to add to `finally` if that is the case
- Works for both "Monadic" types and volatile types like files
  - controls both context entrance and context exit

## Improved Operator Overloading

- move operator overloads outside of interfaces
  - allows for more efficient overloads (defined in terms of functions, makes more sense)
  - operators can be "left-handed" or "right-handed"
  - logically, an operator doesn't have a "primary operand" (when we see `2 + 3`, we don't think `2.add(3)`)
  - applied more as first-class citizens (if you will)
- operator overloads can have an `inplace` form that will be used whenever the operator
is applied to mutable values (where mutation is expected, not where it is possible).
  - allows user to remove unnecessary copying and create more efficient forms of the operators
  - if no `inplace` form is provided, the compiler will use the standard form.
  - if only an `inplace` form is provided, said overload is only valid where the `inplace` form
  would be accepted.
  - immutable values (such as lvalues on the rhs of an expression) may be passed to `inplace` forms
  provided the argument in their position is marked `const` (allows for compound assignment forms
  of the operator work as desired).
    - the compiler should determine this immutablility reasonably
  - specified by a `#inplace` annotation
- **all** operator overloads aggressively elide copies (even in violation of Whirlwind's value
semantics - eg. the underlying array of a list will not copied even if the operator is called
as a function)
  - all arguments to `inplace` operator overloads will not be copied
  - all const arguments to standard operator will not be copied
  - the compiler may selectively elide copies on non-const values passed to
  standard operator overloads if the argument is treated as a constant or if
  the value being passed is an rvalue.
  - this total copy elision does **NOT** propagate beyond the argument

## Type System Adjustments

- make init lists main way to initialize structured types
  - don't have to provide values for everything
  - allow for `...` operator to initialize a struct based
  on a previous struct (called spread operator)
    - looks like `Struct{...s, x=10}`
    - emulates Elm struct value update syntax (kind of)
    - can only spread on struct of same type
- all typesets have **no** null value
  - compiler should error if a null is used to satisfy a typeset
  - interfaces and `any` are considered typesets (reminder)
  - this includes anywhere where null is implicit (eg. null initialization)
    - if the compiler can determine that null value is never used (ie. open-form initialization)
    before it given a proper value, the compiler should not throw an error

## Syntactic Sugar and Smaller Adjustments

- heap alloc synactic "overhaul"
  - to allocate types: `make for type`
  - to allocate a block of types: `make for type * numtypes`
  - to allocate a value `make expr`
- allow for stacked annotations
- revised iterator syntax
  - use `in` instead of `<-` (easier to type and looks better)
  - use `for` instead of `|` in comprehensions (looks better, removed ambiguity)
  - "Python style"
- partial function calling (replacement for old partial function syntax)
  - `f(_, 23, _)` creates a function from f that accepts the two arguments left blank
  - can allow for "currying" (not actually but...)
  - more clear than implicit currying/argument omission (re. Haskell)
- support for async iterators (possibly need a better name)
  - `async for` loops (not in comprehensions, too complex)
  - fits in with rest of language
  - mostly syntactic sugar and special iterator class
- allow for `yield` to be used in tandem with `return` to prompt the program
to return the yield-value early
  - if an empty `return` occurs after a valid (and deterministic) `yield`,
  the `return` causes the function to return the yielded value
  - unambiguous since `yield` can only be used with value-returning functions
  and empty `return` can only be used with non-value-yielding functions

## Internal Adjustments

- intrinsic implementation
  - prevent intrinsics from being converted to first class functions
  - allow for generic intrinsics
  - `#intrinsic` annotation (implementation)
- inline method calls (as much as possible - may only be possible in type interfaces)
- make `ctx_strand()` intrinsic and access the current running strand from TLS
- classifier values (cvals) should be i32 not i16
  - alignment of all data structures where they are used means the
  memory that would be saved is padded away anyways
- builtin collections implement as references to special type declarations
  - `[]T` -> `core::__array<T>`
  - `[T]` -> `core::__list<T>`
  - `[K: V]` -> `core::__dict<K, V>`
  - implement initializations as such
- copy and allocation elision
  - avoid copying and/or allocating wherever possible
    - constant function arguments do not need to be copied
    - rvalues do not need to be copying before being passed to a function
    - constants do not require an explicit `alloca` to create them
    - constants should only be copied if not doing so would compromise their constancy
    - ET CETERA (there are more instances!)
  - general rule: the compiler should only enforce pure value semantics when not
  doing so would have an apparent effect on the behavior of the user's code
    - eg. a mutable list when passed to a function must be copied because
    the user expects to be able to mutate the list inside the function without
    mutating their outer list
    - however, if the list is an rvalue or being passed as a constant, eliding
    the copy has no effect on that actual behavior of the program (it just makes
    it faster)
    - note: behavior is not the instructions executed or how they are executed, but
    rather the actual task performed by the program

## Compiler UX

- more friendly error messages
  - include category, shorten lines, add suggestions where possible
  - specific descriptions
  - separate position an file (see example errors file in `Whirlwind Notes`)
  - give error, line, column, and display highlighted code with line numbers
  - **consider** colored text
