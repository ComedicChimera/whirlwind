# Memory Model

This document outlines Whirlwind's memory model, some of the philosophy behind it, and
some implementation details of it.

This model was inspired by a paper by David Gay which was published during his time at
Stanford University entitled *Memory Management with Regions*.  A PDF of this paper can be
found [here](https://theory.stanford.edu/~aiken/publications/theses/gay.pdf).  It is a
good read and very helpful for understand why I chose to approach some aspects of the
memory model the way I did.

## References

A reference is like a pointer, but it cannot be treated as a numeric value.

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
      - Block references are actually references to arrays (which contain their
        own references)
        - This means that operations like `resize` don't cause null errors on
          duplicate references since it mutates the internal reference -- not
          the reference all the block refs share
      - There is also a `copy` function to copy the data from one block
        reference to another
        - Eg. `copy(src, dest)`
        - Equivalent to `*r = data` for normal references
        - Both references must be the same size
      - Finally, there is a `move` function that copies the contents of one
        reference into another and then deletes the contents of the previous
        reference.      
        - This also adjusts the source block reference to point to the data in
          the destination reference
          - This prevents null errors (again since the internal pointer is
            mutated/isolated)
        - Both references must be the same size
      - `&[]type` and `[&]type` are NOT the same
        - Block references are implicitly owned and point to the heap
        - References to arrays are simply that: a reference to some array
          somewhere
          - The array may or may not be on the heap
          - It cannot be resized
          - `&[]type` is considered a stack reference

## Regions 

A region is a large "block" of memory that holds the contents of a large number of dynamic references

  - Regions are equivalent to scopes but for dynamic memory
    - Individual dynamic references CAN NOT BE DELETED
      - Block references can be "resized" which does entail a reallocation, but
        they are only "deleted" during the window in which they are being
        resized
    - Regions are deleted as one discrete block -- they can NOT be explicitly
      deleted
    - Regions can contain sub-regions (and sub-regions can contain sub-regions
      like scopes)
      - A region can only be deleted once all of its sub-regions have been
  - Regions are always deleted at the end of the scope in which they are created
    - This makes their behavior predictable both to the compiler and programmer
    - The primary exception is global region which is discussed later
  - Regions can be treated as literal values
    - Passed to functions, stored in variables, etc.
    - This is essential for facilitating a dynamic memory model (that can change
      as the program flows different directions)
      - used to allow allocation at an indefinite level
  - Regions can be created implicitly or explicitly
    - Using the `local` allocation specifier creates a region local to the
      current function if a region has not already been created
      - If a local region already exists, then no new region is created an
        `local` simply points to that region
      - This specifier is discussed in more detail in the *Allocation* section
    - `region r local` creates a new local region that can be accessed and
      referred to as `r`
      - If a local region already exists, then this syntax simply exposes a
        reference to it named `r`
    - `region r of` specifies a new region that exists for the scope of the `of`
      block
      - Used to more tightly subdivide the memory of a function
      - An explicitly defined region can NOT be created within another
        explicitly defined region
        - Avoids unnecessary complication and prevents "unidiomatic" programming
          practices 
      - It is referenced by the name `r`
        - The `r` can be elided (as `local` points to it within the `of` block)
  - All regions are considered to a have a "rank" which determines at what level
    of the region hierarchy they exist
    - Higher rank = further down
  - A reference's region is not explicit in its type, but rather stored as a
    contextual tag

This method efficiently prevents memory leaks since the user can freely assign to reference
variables and "lose" references to earlier data since it will all be cleaned up when the region
is deleted.  No reference counting is necessary due to nullability (discussed later).

## Allocation

Dynamic references are allocated in an explicit region when they are created.  

  - These references can be created using the `make` syntax which is structured
    as follows:
    - `make region-specifier allocation-parameter`
  - The "region-specifier" defines in what region relative to the allocation the
    reference will be created in.
    - `local` specifies that the reference is created in the current region
      - this exists for additional concision (allocated in a local region is
        common)
    - `in[r]` allows one to specify a region explicitly for allocation
      - `r` is an identifier with a type of `region`
      - It must be an explicit region reference
  - The "allocation-parameter" determines what dynamic reference is produced
    - When this is a type, an owned reference is produced
      - Eg. `make local int` produces an `own &int`
    - When this is a tuple of a type an a value, a block reference is produced
      - Eg. `make local (int, 10)` produces an `[&]int` of a 10 elements
    - When this is a value (structured type), it will allocate enough memory to
      store that value and then store it in that allocated memory
      - Eg. `make local Struct{v=10}`
  - The compiler will enforce region locking to prevent null-dereferences
    - A reference created in a specific region will not be allowed to "leave"
      that region
      - Eg. you can't return a `local` or `nonlocal[region]` reference from a
        function
      - You can't put a standard reference in the global region
      - You can't allocate a variable locally in an explicit region and store it
        in a variable in a region higher up on the stack.

## Nullability and Static Rank Analysis

While regions effectively circumvent memory leaks, they do not solve the larger problem of
null reference errors -- returning or accessing references that no longer exist.  While
regions can help the compiler to analysis where a reference belongs in the hierarchy, 
they are not deterministic since the control flow of the program shifts so regularly.
Therefore, we have to implement a "coping mechanism" for when the compiler doesn't know
what region a reference specifically belongs to -- this mechanism is nullability.

It is worth noting that enforcing strict region consistency/region locking (eg. forcing all 
references returned from a function to be of the same rank) is not feasible in the general case
as many applications require complex control flow that can't be predicted accurately enough for
such a system to work effectively.

Static rank analysis is the main tool the compiler has to detect where null reference errors are
possible.  For each reference "position" (eg. in a return type, in struct field, etc), the
compiler determines a set of possible relative ranks that reference can be.

Relative rank is a method for determining rank based on other given variables.  For example,
say a function `fn` is defined as follows:

```
func fn(r: region) own &int do
  if some_cond do
    return fn(r)

  return make in[r] int
```

The region `r` accepted by the function as an argument is said to have an arbitrary rank `n`.
The base case of the function returns a reference allocated in `r` which implies that the
returned reference has a rank of `n` as well.  The recursive case simply calls the function
again with `r`.  Since we know that in the base case, the function maps a region of rank `n`
to a reference of rank `n` (ie. the function is `n -> n`), we know that in this case, for 
region of rank `n`, the returned value must also be `n` (the recursive case is not considered
again).  Therefore, the function as a whole is `n -> n`.  The rank `n` is said to be a relative
rank.

In the case above, there is only relative rank in play: `n`.  This might not always
be the case:

```
func fn2(r1, r2: region) own &int do
  if some_cond2 do
    return make in[r1] int

  return make in[r2] int
```

This function takes in two regions of relative rank `n` and `m` respectively and returns a reference
that can either be `n` or `m`.  Therefore, the set of possible ranks of the return reference is
`{ n, m }` and the function is said to be `(n, m) -> { n, m }`.  

If all possible ranks of a reference are exceeded (eg. ranks {n, n+1} and you are now in rank n-1), then
all usage of that reference is forbidden as it will always be null.  However, if only some of the possible
ranks are exceeded (eg ranks {n, n-2, n+3}, you are now in rank n-1), then the reference is considered nullable.
Nullability must be marked as a part of the data type in all future/higher rank usages of it.  For example, if
a function returns (or could return) a nullable reference, the type of that reference must be marked with a `?`
at the end of its type so that the nullability is made obvious to the reader.  =

TODO: figure out a system for nullability that works for data structures.

## Nullable Operators

Nullable operators must be used to access nullable references as a measure to check for null errors.

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

All references may use null operators as necessary, but do not have to unless they are nullable.

## The Global Region

There exists a region known as the global region that is allocated globally and 
is not affiliated with any particular Strand or scope.

  - Memory can be allocated here using the region specifier `global`
  - References allocated in the global region have a special type specifier:
    `global`.
    - Both owned references and block references can be made global by putting
      this specifier before their definition
    - Eg. `global own& int` or `global [&]float`
    - Free references cannot be marked as global
  - You can NOT borrow a global reference
    - This is to avoid null-reference errors
  - Unlike references in all other regions, global references CAN be deleted and
    must be managed explicitly
    - It is impossible to determine a consistent lifetime for them statically
    - They are deleted using the `delete` function which works for both global
      and block references
    - This is part of the reason why global references can not be borrowed and
      have an explicit tag on them
      - Prevents them from being confused with normal references and helps to
        minimize null-reference errors
  - They should only be used when necessary
    - When data must be stored and managed globally
    - When the references is being shared by multiple strands as the global
      region is strand agnostic
      
## Constancy

Constancy is a property of a reference or variable that determines whether or not it
can be mutated.

  - only applies to references and variables (mutable, named values)
  - for variable declarators:
    - `const x ...`
    - constancy will be applied as an optimization after semantic checking where
      possible
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

## Value Categories

Like C++, Whirlwind does use value categories to help distinguish between what things can be
referenced and what things can't be. 

  - lvalue (well-defined, mutable value, able to take a free reference to it)
  - rvalue (undefined/poorly-defined, immutable value, unable to take any kind of reference to it)

These categories are generally simpler than those present in C++ (only two categories) as
Whirlwind's data model is a lot simpler.

## Dealing with Heap Fragmentation

Regions have a minimum size of a single page (but can hold as many pages as is necessary).  This means that frivolously
creating regions can lead to a good deal of heap fragmentation since large portions of regions may go unused.  This is
why the compiler can not automatically create regions even in correspondence with other structures (such as lexical
scopes, functions, etc.) as there is no way for it to effectively predict whether or not a whole region is necessary.

The programmer is responsible for creating and managing regions efficiently.  It is impossible/infeasible for the compiler 
to accurately assess the flow of a complex program in the general case so we must pass this mental load onto the programmer.  
It is still preferrable to manually managing individual references and is, in most cases, a fairly reasonable expectation.