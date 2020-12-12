# Type System

Whirlwind's type system is a bit more complex but also more rich than that of
most other languages.  It features several concepts familiar to functional languages
and attempts to, in some respects, distance itself from the standard OOP type
model while still enabling some of its more powerful features.

Whirlwind is strongly and statically typed; however, there is an `any` type for
situations in which the powers of dynamic typing are required.

## Fundamental Types

Primitives
: These are the standard "simple" types builtin to most languages including
: various sizes of integer and floating point numbers, a character type, a string
: type (strings are considered primitive in Whirlwind), and a boolean type.

References
: See explanation of the reference type in the [Memory Model Documentation](memory-model.md)

Tuples
: A tuple is an ordered n-tuple of variously typed values.  Eg. the pair `(int, string)` is
: a tuple with an integer in the first position and a string in the second position.

Vectors
: A vector represents a fixed-length set of numeric values.  This type is used for efficient
: and idiomatic SIMD computations.  There is a [section on vectors](#vectors) toward the
: bottom of this document explaining them more throughougly.

Structures
: A structure or struct is modeled after a C-style struct type.  It is a record of named,
: typed fields that can be accessed and mutated.  

Interfaces
: An interface a type used to group types based on behavior.  For an explanation on
: how they work, consult the [Polymorphism and Interface Binding](#polyinterf) section
: of this document.

Function Types
: A function type represents a first-class function "object" (not in the OOP sense)
: that can be called, passed, and composed.

Algebraic Types
: An algebraic type is a type defined with multiple discreet values/instances.  For
: a more in depth explanation of this type, consult the [Algebraic Types](#algebraic) section.

## Builtin Types

Whirlwind also features several important builtin types that are
used for collections or sequences.

Arrays

Lists

Dictionaries

## <a name="polyinterf"/> Polymorphism and Interface Binding

## <a name="algebraic"/> Algebraic Types

## Generics

## <a name="vectors"/> Vectors

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

## Adjustments from Previous Versions

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