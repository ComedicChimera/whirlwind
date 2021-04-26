# Considerations

This file contains proposed features for Whirlwind (so I don't forget them).  These
are things that I am not sure I want to in the language or want to change about the
language but that I do want to remember for later.

## Pass Sequences as Indefinite Arguments

- Syntax: `fn(...seq)` (Go-style)
- Whirlwind implements indefinite arguments as "arrays" internally anyway

## Internal/Private Fields and Methods

- Could be true privacy or internal to the package
- Using a `priv` keyword or similar

## Dependent Methods

- Methods only appear on certain generates of an interface
- Allows for more direct type decomposition
- Examples: `sum` or `to_dict`
- Sort of like our old idea for "method specialization" as a separate concept from function specialization

## Monad Reworks

A revised set of syntactic sugar for working with monads (for error handling and call functions)

Firstly, monads can establish and manage context through their binding functions.

Eg. the `File` monad frees its file handle when exiting its context.

### For Expressions

- Add `?` operator for monadic accumulation
- `sqrt(x)? + 2` yields value `Some(sqrt(x) + 2)` if `x >= 0` and `None` if `x < 0`
- Sort of like pervasive null accumulation

### For Context Managers

1. Allow for monads to establish full context (and close it) over a block
2. Handle errors at each individual bindings
3. `else` case runs if any layer fails to pass

For example, monadic file IO:

    with
        # open file
        fres <- fopen("file.txt") ?? handler_func
        # acquire file from "file resource"
        file <- fres  
    do
        ...
    else
        println("Failed to open file.")

Two Notes:

- Find a way to remove that middle stage (or acquiring the file)
  * Maybe just have file not just be a regular `Option` type...?
  * Merge multiple monadic binds into one?

## Implicit Abstraction

- Change back to the old style of partial function calling: `|f 2, 3, _)`
- Add support for "ignore-remaining-args": `|f 2...)`
- Add operator functions: `|+)` invokes the plus operator
  * Should work with the other elements of the type solver



