# Adjustements

List of minor adjustments from previous versions of Whirlwind.

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

## Syntactic Sugar and Smaller Adjustments

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
