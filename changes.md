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
- memory model
  * three kinds of pointers: stack, dynamic, volatile
  * stack pointers have standard stack lifetimes unconditionally
  * dynamic pointers have compiled-determined and/or user-specified lifetimes
  * lifetimes can be conditional (eg. one branch of if returns value, other deletes it)
  * if the compiler cannot determine the lifetime of the resource, then error
  * if the use of a resource is inconsistent with its specified lifetime, then error
  * `delete` statement used to ensure resource has a proper lifetime
    - delete must not violate an established lifetime
    - used when the compiler thinks a resource may not be able to user-managed (see examples)
  * `always` statement used to "prove" that a loop or conditional branch always occurs
    - like a promise to the compiler
    - can cause the compiler to identify and error on deadcode
  * `own` keyword used in certain contexts to indicate that a value's lifetime is bound to its current scope
    - eg. function arguments
  * all dynamic allocations in any particular scope must have an "owner"
    - eg. if a function returns a dynamic value, but that value is not stored anywhere that is an error
  * use of move operator (`:>`) for efficient dynamic assignment (and deletion)
    - similar semantics to delete operator
  * all lifetimes are compiler-managed on a resource level (not name level)
    - eg. you can assign to a resource's owner without a move call (bad practice) without necessarily violating
    the lifetime b/c the compiler simply caches that resource if its lifetime needs to be enforced
    - may be a problem if the compiler determines that that resource has an inconsistent lifetime as a result
  * null pointer errors handled in two ways
    - regular dereference operators on both known kinds of pointers have checks that cause runtime panics
    if null pointer is encountered
    - nullable dereference operators have same behavior as regular but return null instead of panicing
  * assignment is only valid in certain situations:
    - it doesn't cause a lifetime violation (see docs)
    - it doesn't lead to an inconsistent or indeterminate lifetime
  * move is valid everywhere delete is valid and:
    - when deletion would cause an indeterminate lifetime (see example at end of docs)
    - ie. it is only valid on direct resource holders (where the lifetime is predetermined)
  * see examples in docs
- move operator overloads outside of interfaces
  * allows for more efficient overloads (defined in terms of functions, makes more sense)
  * operators can be "left-handed" or "right-handed"
  * logically, an operator doesn't have a "primary operand" (when we see `2 + 3`, we don't think `2.add(3)`)
- PLUS: all the other changes that can be observed in grammar
