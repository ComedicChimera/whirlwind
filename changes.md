- make errors include positions (TokenError, ASTError, etc.)
- `closed` type definitions
- remove `->` operator, just use `.` for everything, update `?->` to `?.` accordingly
- overload `?.` for interfaces
- find some way of optimizing/simplifying interfaces
- split up loops into two categories: `for` and `loop` 
  * `loop` is for infinite and conditional loops `loop (expr) { ... }`
    and `loop { }` for infinite loops
  * `for` is for iterative for loops (c-style, iterators)
- change expression local syntax to `let x in expr`
- remove `then`
- remove necessity of `new` keyword (can keep `make` syntax tho)
- remove constructors (can be implemented as NewType() methods if really necessary)
- make init lists main way to initialize structured types
  * allow for unnamed entries
  * don't have to provide values for everything
- constancy only applies to references and variables (mutable, named values)
  * for variable declarators:
    - `const x ...`
    - `const x in ...`
    - constancy will be applied as an optimization after semantic checking
      where possible
  * same syntax for structs and function arguments
  * for references:
    - `&const x` (create a const reference to x)
    - `const dyn* x` (constant dynamic reference)
    - `const* x` (const stack reference)
    - `const vol* x` (constant volatile reference)
  * reference constancy is viral
    - `let x = &const y;` (x is now a const reference)
    - x can still be mutated, the reference cannot be
  * value constancy is not
    - `const x = 10; let y = x;` (y is not constant)
  * casting rules: mutable -> constant, constant -/> mutable
  * methods can be constant (explicitly)
- cannot take a non-const reference to a constant value
- remove `static`
- value categories:
  * lvalue (well-defined, mutable value, able to take both kinds of references to it)
  * cvalue (well-defined, immutable value, only able to take a const reference to it, value constancy)
  * rvalue (undefined, immutable value, unable to take any kind of reference to it)
- partial function calling (replacement for old partial function syntax)
  * `f(_, 23, _)` creates a function from f that accepts the two arguments left blank
  * can allow for "currying" (not actually but...)
  * more clear than implicit currying/argument omission (re. Haskell)
  * another use of the `_` in a logical way
- ownership model:
  * heap memory managed by lifetimes (no explicit deletion)
  * ownership is a value property that must be specified explicitly
  * propagates across '=' when coming from an r-value, but not from an l-value or a c-value
  * move (`:>`) valid on owned reference, set (`=`) valid on unowned references
  * ownership is a value property that must be matched during initialization
  * ways to use move
    - reposition (classic move, `x :> p`)
    - reallocate (move to a new value, `x :> make int`)
    - "delete" (move to null, `x :> null`)
  * exception: `vol*` ignores ownership semantics (not a good idea if not necessary)
  * lifetime closing (delete) won't cause issues on null pointers (if move to null
  deterministic, delete omitted; if not delete set, null checked)
  * ownership CAN be transferred
    - must be done explicitly: `own(p)`
    - in situations where Whirlwind cannot determine path of ownership deterministically,
    it treats all possible owners as true owners and generates conditional lifetime semantics
    - explicit transfer not necessary if no prior owner exists (eg. `f(make int)`)
- PLUS: all the other changes that can be observed in grammar
