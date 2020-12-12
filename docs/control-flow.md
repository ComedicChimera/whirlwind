# Control Flow

This document outlines the basics of Whirlwind control flow.

## Loops

There are two kinds of loops:

  - `while` is for conditional loops
  - `for` is for iterator-based loops

These loops can each be used with `break` and `continue` as in
standard programming languages.

They also both allow for a `nobreak` suffix that will execute
if the loop runs unbroken.

## Pattern Matching

Whirlwind supports pattern matching using the `match` statement and expression.

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