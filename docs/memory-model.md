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
      - Block references are actually references to arrays (which contain their own references)
        - This means that operations like `resize` don't cause null errors on duplicate
        references since it mutates the internal reference -- not the reference all the block
        refs share
      - There is also a `copy` function to copy the data from one block reference to another
        - Eg. `copy(src, dest)`
        - Equivalent to `*r = data` for normal references
        - Both references must be the same size
      - Finally, there is a `move` function that copies the contents of one reference into another
      and then deletes the contents of the previous reference.      
        - This also adjusts the source block reference to point to the data in the destination reference
          - This prevents null errors (again since the internal pointer is mutated/isolated)
        - Both references must be the same size
      - `&[]type` and `[&]type` are NOT the same
        - Block references are implicitly owned and point to the heap
        - References to arrays are simply that: a reference to some array somewhere
          - The array may or may not be on the heap
          - It cannot be resized
          - `&[]type` is considered a stack reference

## Regions 

A region is a large "block" of memory that holds the contents of a large number of dynamic references

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
      - A function can be made up of multiple regions of equal scope.
        - There regions all have the same "rank" (that is they are all subregions of the same region)
        - All such regions of equal rank are deleted when the function exits.
      - If a function does not actually require its own region, then no region will be created
    - Regions can also be defined explicitly using the `region of` syntax (defines a new block
    and lexical scope)
      - These regions are consider a sub-region of their enclosing function
      - An explicitly defined region can NOT be created within another explicitly defined region
        - Avoids unnecessary complication and prevents "unidiomatic" programming practices 
  - A reference's region is not explicit in its type, but rather stored as a contextual tag

This method efficiently prevents memory leaks since the user can freely assign to reference
variables and "lose" references to earlier data since it will all be cleaned up when the region
is deleted.  No reference counting is necessary since as region locking is enforced (as discussed
in the next section).  

## Allocation

Dynamic references are allocated in an explicit region when they are created.  

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
    - `in[r]` allows one to specify a region explicitly for allocation
      - `r` is an identifier with a type of `region`
      - the current region can be accessed in "literal" form using the `ctx_region` function
        - `ctx_region` can not be used in global namespace (unable to get a reference to the global region)
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
      - You can't allocate a variable locally in an explicit region and store it in a variable
      in a region higher up on the stack.

# Region Consistency

Region consistency is an axiom of Whirlwind's memory model that states that along any given
codepath, the region to which a reference belongs must be consistent.

This also implies that the compiler should be able to work out the regions each reference
belongs to *statically*.

For example,

```
func my_func do
    let r: own &int

    // ERROR: region of r is not consistent
    if some_cond do
        r = make local int
    else
        r = make nonlocal int

    // in what region is `r` out here?
```

We can see that because `r` does not have consistent region, any error is thrown.

> Additionally, you can't dereference an uninitialized reference so accessing `r` before
> allocation or along a code-path in which is may not have been allocated results in an error.

Function return types have the same property as variables.  This is essential because it
allows us to determine the region of the return value of the reference allowing us to
predict where a returned reference is accurately.

However, data structures to do not have the same rules:

```
type Store {
    r: own &int
}

func f1 do
    let s = Store{r = make local int}

    // -- snip --

func f2 do
    let s = Store{r = make nonlocal int}

    // -- snip --
```

An owned reference field of a struct can house many different kinds of owned references.  However,
region consistency MUST be observed across instances.

Block references must also obey reference consistency.  Moreover, for both kinds of
references, the compiler should use its knowledge of where a reference is allocated to
prevent null dereferences (ie. you can't use a reference after its region has been deallocated).
You also cannot resize a block reference to a reference outside of its original region.

A final quirk of region consistency is that it does NOT necessarily have to observed linearly:

```
func my_func own &int do
    let r = make local int

    // -- snip --

    r = make nonlocal int

    // -- snip --

    return r
```

The above code contains no errors because although the region of `r` changes, it changes deterministically
meaning that along the code path above, `r` has a predictable and consistent region.  This is why
we choose to use the idea of region *consistency* rather than region *constancy*.

Note: All methods of an interface that return dynamic references must return said references in regions of
equal rank (or rather an equal rank offset).

Note: For situations where functions may need to return data structures of equal rank, make sure that region
consistency works properly for algebraic types eg. our linked list examples.

## The Global Region

There exists a region known as the global region that is allocated globally and 
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

## Nullable Operators

Nullable operators can be used on all references to help prevent null-reference errors.

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

## Constancy

Constancy is a property of a reference or variable that determines whether or not it
can be mutated.

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

## Value Categories

Like C++, Whirlwind does use value categories to help distinguish between what things can be
referenced and what things can't be. 

  - lvalue (well-defined, mutable value, able to take a free reference to it)
  - rvalue (undefined/poorly-defined, immutable value, unable to take any kind of reference to it)

These categories are generally simpler than those present in C++ (only two categories) as
Whirlwind's data model is a lot simpler.

## Region Static Rank Analysis

While region analysis when the body of single function is fairly trival, when applied across multiple
functions it can be very difficult.  Moreover, analysis across multiple functions can be quite complex
and since functions are called in a variety of different contexts, it is profoundly inefficient to simply
recompute the rank of the resulting region on every single call.  Thus, the return values of functions have
a rank that is computed as a function of some inputs.  For example, for an input region of rank `n`, the function
returns an owned reference of region `n`.  Or for an enclosing region of rank `n`, the function returns a reference
of rank `n + 1`.  This does complicate the representation of regions the `rank` field stored in their data type.

Additionally, it should be obvious that any recursion would cause issues for a naive analyzer.  Thus, another benefit
of this functional representation is that parameters and return values can be variable.  In effect, we can say that
the return value has a rank of `m` and determine the value of `m` over time by relating all uses of that value to `m`.
Ironically, this call also help prevent infinite recursion since if `m` is completely indeterminant than no valid
base case is ever reached and thus an error can be thrown.  Let us consider a simple example to show case what is
meant by "variable rank"

```
func fn(r: region) own &int do
  if some_cond do
    return fn()

  return make in[r] int
```

In the above code, `r` is said to have a rank `n` and `own &int` (the return) is said to have a rank of `m`.  In the
first conditional branch, `m` is related to itself and thus no change occurs.  In the second branch, however, `m` is
related to `n` and thus the determined value of `m` is `n`.  Thus the relational signature of our function is: `n -> n`.

Several other data types such as structs have similar relational properties to functions (their fields store references of 
rank `n` where `n` is determined on a use by use basis).


