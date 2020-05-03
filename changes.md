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
  * `make` returns a mutable reference, but expects to be initialized in
    a constant value (ie. a constant variable)
    - includes all data structures (must be a constant valued data structure)
  * methods can be constant (explicitly)
- const references can be realloced (moved) (?)
  * `make ... to size` is still valid (provided the size is multiple of the type)
- cannot take a non-const reference to a constant value
- remove `static`
- value categories:
  * lvalue (well-defined, mutable value, able to take both kinds of references to it)
  * cvalue (well-defined, immutable value, only able to take a const reference to it, value constancy)
  * rvalue (undefined, immutable value, unable to take any kind of reference to it)
- PLUS: all the other changes that can be observed in grammar
