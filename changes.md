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

## Memory Update (References, Regions, Constancy)

- References and Regions
  - A reference is like a pointer, but it cannot be treated as a numeric value
    - No arithmetic of any kind
    - Double references (and any higher degree) are not allowed
      - References are semantic constructs -- not values
  - Three Kinds of References:
    - Free: `&type`
      - Not affiliated with any particular region
      - Created using the `&` operator -- for stack references
      - Can be created from an owned reference using the `borrow` function
      - Can NOT be created from a block reference
      - These references can NEVER be returned from functions
    - Owned: `own &type`
      - Affiliated with a specific region determined when they are created
        - They are region-locked (owned by a particular region)
      - Used to represent single-valued dynamic memory
        - They are not blocks of memory -- they cannot be resized
    - Block: `[&]type`
      - Also affiliated with a particular region when created
      - Used to represent a block of memory (as a psuedo-array)
        - Can be moved and resized
      - A block reference may store other references as its element
      - However, an reference may not have a block reference as its stored type
      - Resized using the `resize` function -- works in-place
        - Eg. `resize(block, 10)` -- resizes a block to store 10 elements
  - A region is a large "block" of memory that holds the contents of a large
  number of dynamic references (sort of like a page)
    - Regions are equivalent to scopes but for dynamic memory
    - Individual dynamic references CAN NOT BE DELETED
      - Block references can be "resized" which does entail a reallocation,
      but they are only "deleted" during the window in which they are being resized
    - Regions are deleted as one discrete block -- they can NOT be explicitly deleted
    - Regions can contain sub-regions (and sub-regions can contain sub-regions like scopes)
      - A region can only be deleted once all of its sub-regions have been
    - Each function in addition to defining a stack frame also defines a region
      - Sub-regions expand out from the parent region of the main function
      - When a function exits, its region is deleted/destroyed (assumes all sub-regions
      have also been destroyed)
    - Regions can also be defined explicitly using the `region of` syntax (defines a new block
    and lexical scope)
      - These regions are consider a sub-region of their enclosing function
      - An explicitly defined region can NOT be created within another explicitly defined region
        - Avoids unnecessary complication and prevents "unidiomatic" programming practices
  - Dynamic references are allocated in an explicit region when they are created
    - Their region is not explicit in their type, but rather stored as a contextual tag
    - These references can be created using the `make` syntax which is structured as follows:
      - `make region-specifier allocation-parameter`
    - The "region-specifier" defines in what region relative to the allocation the reference will
    be created in.
      - `local` specifies that the reference is created in the current region
      - `nonlocal[func]` specifies that the reference is created in the region enclosing
      its parent function
        - The `[func]` can be elided whenever the context is unambiguous (ie. the allocation
        is not occurring within an explicitly defined region)
      - `nonlocal[region]` specifies that the reference is created in the region of its
      parent function 
      - `nonlocal[r]` allows one to specify a region explicitly for allocation
        - `r` is an identifier with a type of `region`
        - the current region can be accessed in "literal" form using the `ctx_region` function
        - used to allow allocation at an indefinite level
    - The "allocation-parameter" determines what dynamic reference is produced
      - When this is a type, an owned reference is produced
        - Eg. `make local int` produces an `own &int`
      - When this is a tuple of a type an a value, a block reference is produced
        - Eg. `make local (int, 10)` produces an `[&]int` of a 10 elements
      - When this is a value (structured type), it will allocate enough memory
        to store that value and then store it in that allocated memory
        - Eg. `make local Struct{v=10}`
    - The compiler will enforce region locking to prevent null-dereferences
      - A reference created in a specific region will not be allowed to "leave" that region
        - Eg. you can't return a `local` or `nonlocal[region]` reference from a function
        - You can't put a standard reference in the global region
  - Functions returning non-global owned references must indicate to what region those references
  belong.
    - `local` => the reference belongs to the region of the caller
    - `nonlocal` => the reference belonds to a region above the caller
      - Can be returned as a `local` reference from any function that received it as `nonlocal`
      - This handles explicitly specified region allocation
  - Finally, there exists a region known as the global region that is allocated global and
  is not affiliated with any particular Strand or scope.
    - Memory can be allocated here using the region specifier `global`
    - References allocated in the global region have a special type specifier: `global`.
      - Both owned references and block references can be made global by putting this
      specifier before their definition
      - Eg. `global own& int` or `global [&]float`
      - Free references cannot be marked as global
    - You can NOT borrow a global reference
      - This is to avoid null-reference errors
    - Unlike references in all other regions, global references CAN be deleted and must
    be managed explicitly
      - It is impossible to determine a consistent lifetime for them statically
      - They are deleted using the `delete` function which works for both global and
      block references
      - This is part of the reason why global references can not be borrowed and have
      an explicit tag on them
        - Prevents them from being confused with normal references and helps to minimize
        null-reference errors
    - They should only be used when necessary
      - When data must be stored and managed globally
      - When the references is being shared by multiple strands as the global region is
      strand agnostic
  - Nullable operators can be used on all references to help prevent null-reference errors
    - The null test operator is used like so: `ref?` where `ref` is some reference and
    accumulates that reference to `null` if it has already been deallocated/points to
    garbage memory or is itself `null`
      - Sort of like a null-coalescing operator
      - Intended to be used like so: `ref? == null` to test if a reference is null
    - Several operators including `.` and `[]` can be "paired" with a null test operator
    to prevent them from throwing errors on null references
      - The `?` is placed before the operator like so: `?.`
      - It produces an `Option` type representing the value of the operation
      - It runs a null test before the operator, and if the reference the operator would be
      acting on is not safe for use (eg. it has already been deallocated), it will
      produce a `None` value.  If it is safe for use, then it will be produce a `Some` value
        - Will not stop any non-memory-related panics (eg. index out of bounds)
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
  - lvalue (well-defined, mutable value, able to take a free reference to it)
  - rvalue (undefined/poorly-defined, immutable value, unable to take any kind of reference to it)

## Vectors

- vector constructor: `<value : const_len>` or `<{elems...}>`
- vector data type: `<size>type` (only valid on integral, floating point or pointer types)
- vectors can be used generically using the `Vector` built-in type set
  - eg. `T: Vector<int>` (gives all `int` vectors regardless of size)
- all basic arithmetic operations are valid on vectors (scalar and vector mult)
- additional intrinsics and utilities (eg. `__vec_sum(v)` and `__shuffle_vec(v1, v2, mask)`)
- `#vec_unroll` annotation to cause vector functions to be optimized (as much as possible)
- extended (later) as part of math library (intended for general purpose use, also used in matrices and complex numbers)
- vector array initializers should be compiled as `shufflevector` if possible
- VECTORS ARE ITERABLE

## Context Managers

- Used for concise unpacking and chaining of monadic types
  - Effectively enforces a monadic context
- Exists as a block statement and as an expression
  - Block: `with monadic_context do`
  - Expression: `with monadic_context => expr`
    - Type of expression and type of monadic context must be same
  - Monadic Context is a series of binding expressions:
    - `a <- monadic_value`
    - Each one fails if the previous fails
- Block has an `else` that will be run if the context isn't established
  - Can be a plain `else` that justs naively runs
  - Or it can pattern match with `else match to` which will allow you
  to match over the failed value
- Expression has no else clause => simply accumulates to the condition
that failed
- Used for avoid endless match statements

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
- region deletion should be performed by the Strand who owns the section the region
is in (not concurrently)
  - avoids endless livelocking w/ the memory deletion thread
  - TO BE FURTHER EXPLORED

## Compiler UX

- more friendly error messages
  - include category, shorten lines, add suggestions where possible
  - specific descriptions
  - separate position an file (see example errors file in `Whirlwind Notes`)
  - give error, line, column, and display highlighted code with line numbers
  - **consider** colored text
