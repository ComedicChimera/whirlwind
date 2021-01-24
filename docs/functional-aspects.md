# Functional Aspects

This document outlines *some* of the functional programming aspects/features of Whirlwind.

## Partial Function Calling

A form of implicit abstraction that allows for efficient transformations/repurposings
of preexisting functions for new scenarios.

- `f(_, 23, _)` creates a function from f that accepts the two arguments left blank
- can allow for "currying" (not actually but...)
- more clear than implicit currying/argument omission (re. Haskell)

## Context Managers

A context manager is a tool used for concise unpacking and chaining of monadic types.

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
- A clean-up method can be provided to be called whenever the context
manager exits (even in the case of a return).
- All types implement the `Contextual<T>` interface which defines the
following methods:
  - `chain<R>(f: func(T)(R)) (R, bool)`.  `f` is the function to chain after,
  bool indicates whether or not the chain was broken at any point.  `R` is
  chained value returned.  This method is abstract.
  - `exit`  This is called whenever the chain ends -- success or fail.  This
  method is virtual (defined empty in the parent)
