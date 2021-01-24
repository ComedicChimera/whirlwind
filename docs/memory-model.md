# Memory Model

This file documents the workings of the memory model -- how it is to be
understood.  This will function as interim documentation until a more
formal reference/specification is produced.

Concepts in this document may occur somewhat out of order and/or reference
concepts from sections that have not been discussed fully yet.

This memory model was inspired and influenced by the paper on "Memory
Management with Regions" by Dr. David Gay at Stanford University.  It can be found
[here](https://theory.stanford.edu/~aiken/publications/theses/gay.pdf).

## References

A **reference** is a pointer to a value in memory.  In contrast, to other
languages, all references in Whirlwind are not treated like numeric values
-- they are simply ways of referencing a distance value in memory. 

There are several kinds of references: **free references**, **owned references**,
and **block references**.  In addition, both owned and block references can
be local or global.

References may NOT reference other references -- double and triple references
are not allowed.  However, block references may contain other references as
their element type.

Both free and owned references can be dereferenced using the `*` operator. 
This allows you to access their value directly.

### Free References

A free reference is a reference to a stack value.  These reference have little
to no semantics applied to them and belong to a stack frame rather than a region.

They are created using the `&` and `&const` operators.  Their type label is simply
`&T` where `T` is the type being referenced.

### Owned References

An owned reference is a reference to a single heap value that belongs to a specific
region.  The region an owned reference belongs to is not explicit in its type but
rather inferred by the compiler.

Owned references are allocated using the `make` allocation syntax (described later).
They have the type label `own& T`.  Conventionally, the `own` and the `&` are put
together, but they do not have to be.

## Block References

*This is intended to be its own section because these are a very complex topic*.

A block reference is a reference to a contiguous block of heap memory.  They also
belong to a region (not declared explicitly) and can be resized and indexed.
Their length is stored as part of their overall data structure.  Because of this,
they perform bounds-checking when indexed.  They behave much like normal collections.

Their type label is `[&]T` where `T` is the element type.  They are also created
using the `make` allocation syntax.

### Resizing

Block references can be resized using the `resize` function which takes the block
reference and a new number of elements as an argument.  Resizing does NOT invalidate
other copies of the same block reference -- the pointer that is actually manipulated
is stored inside the reference itself.  Block references are a reference to a data
structure that contains both an internal pointer to the block memory and a length.
When they are resized, both the fields are manipulated but the outer reference itself
remains untouched.

### Copying

The contents of a block reference can be copied into another using the `copy` function.
Its signature is `copy(src, dest)`.  It does not mutate the contents of the block
reference.

## Arrays

An **array** is a stack-bound construct that stores multiple elements.  It is the stack
equivalent of a block reference but cannot be resized.  It also stores a length and a
pointer and performs bounds-checking when indexed.  These constructs have a stack
lifetime.  They have the type label `[]T` where `T` is the data type.

## Regions

A **region** is a large block of heap memory that acts as a mini-arena.  Regions are
explicitly allocated in and have the lifetime of their enclosing function or method
(*unless given an explicit scope).\They can contain the memory of all references
that are owned by them.  They have a minimum length of a page and increase in increments
of a page -- they have no fixed size and can grow as necessary.

References in Whirlwind cannot be individually deleted (except in the global region).
Instead, references are allocated in a region which is deleted when its lifetime ends.

Regions can be created in a number of ways and can be passed as values.  Here are all
of the proper initializations of regions.

    // Create a new region or reference a preexisting region that is 
    // local to the current function/method/region-scope
    region r local

    // Create a region `r` that exists with the following scope
    region r of
        ...

Notice that in the first case, a new region may not be created if a local region already
exists.  Additionally, in the second case, the region will only exist for the length of
the block.

Regions can be treated as literal values -- so that they can be allocated in as necessary.
They have a type label of `region`.  Both the two regions above were named `r` and can be
referenced by that name.

Regions cannot be stored in collections of any kind.

## Allocation

To allocate a single heap value or empty block reference, we use the `make` allocation syntax.
This syntax has two pieces: the **region specifier** and the **allocation parameter**.

The region specifier can either be `in[r]` where `r` is the region the memory will be
allocated in or `local` to indicate that we want to allocate in the local region.  Note
that if a local region does not already exist, one will be created when this occurs.

The allocation parameter describes how the memory will be allocated.  There are three kinds
of allocation parameters.

1. Single-Type: The allocation parameter is a type label.  A heap reference of that size will
be created.
2. Block-Type: The allocation parameter is `(T, size)` where `T` is a type label and `size` is
an expression evaluating to an unsigned integral value.  This allocates an empty block reference
with an element `T` and a length of `size`.
3. Struct-Type: This is used to allocate a struct on the heap.  It is simply a struct initializer
(ie. `Name{...}` where `...` contains any field initializers and `Name` is the name of the struct
-- can be a full package access).  The amount memory necessary to hold the struct is allocated and
the fields are initialized as specified.

All allocation of this form either returns a block reference or an owned reference.

Here are some examples of allocations using this operator:

    make local string            // => own& string
    make in[r] (int, 5)          // => [&]int
    make local Point2D{x=1, y=3} // => own& Point2D

## Collection Allocation

Many collections including lists, dictionaries, and block references need to be allocated on the heap
and often times with values.  Although block references can already be allocated, they can only be
allocated as empty.  This may not be desireable. 

This allocation method will create the appropriate construct (which may or may not be a reference) and
populate its memory appropriately. 

Its syntax is similar to the `make` initialization, but it instead uses the keyword `new`.  After
which it takes a region specifier and a **content parameter**.

The content parameter can be in one of the following three forms:

1. Single-Type: This parameter allocates an empty collection of whatever type is provided.  This
is used to create things like empty lists which are non-nullable.  This collection must implement the
`HeapCollection<T>` interface.  The data structure itself will be allocated and then its `empty_init`
method will be called.
2. Literal-Type: This parameter takes in a literal of a builtin-collection (either a block reference,
list, or dictionary) and allocates the space for it, and the populates it appropriately.  This parameter
can also be a comprehension.
3. Extended-Literal-Type: This parameter is built-up of a named data type followed by a block reference
content initializer.  This type must implement the `HeapCollection<T>` interface.  A block reference for
its content will be allocated according to the content initializer.  Then, the collection data structure itself
will be allocated, and then created block reference will be passed to its `init` method.

As is explained above, a type can implement the `HeapCollection<T>` interface to allow for collection
allocation using it.  This interface defines two abstract methods: `empty_init` and `init`.  `empty_init`
should appropriately set up all of the data structure's internal state for allocation but not populate
it with an content.  `init` accepts a block reference and populates the data structure appropriately.

Here is the interface definition of `HeapCollection<T>`.

    interf HeapCollection<T> of
        func empty_init 

        func init(ref: [&]T)

Here are some sample allocations with this syntax:

    new local [1, 2, 3]          // => [int]
    new in[r] [int: string]      // => [int: string]
    new local {x for x in 1..10} // => [&]int
    new in[r] Set{"abc", "def"}  // => Set<string>

## Lifetimes

All references have a predetermined **lifetime**.  This lifetime determines how long they will 
continue to exist.  For example, a reference allocated in a region has a lifetime of that
region.  A reference can never be used or placed outside of its lifetime.  For example, you can't
return a reference allocated in a local region nor can you return a free reference.

Lifetimes are calculated relative to the current location -- much like Rust's generic lifetimes.
For example, in the function:

    func update(r: &T) &T
        return r

The lifetime of `r` is not known in a concrete sense.  However, the compiler does know that the
returned reference and `r` have the same lifetime and that the lifetime extends outside the
current function.

All references stored the same variable or returned from the same function must have the same
lifetime.  This is called **lifetime consistency** and is what the compiler uses to determine
whether or not a given return value is valid.  Note that is based on relative lifetime -- if
a function accepts an arbitrary region and always returns a value allocated in that region,
then lifetime consistency holds because the compiler can always determine relatively where
that value was allocated.

Data structures work similarly.  In any given instance of a data structure, the reference placed
in any given field must have same lifetime.  However, this does NOT need to remain consistent
across many different instances.  Eg.

    type Test {
        ref: own &int
    }

    func fn(r: region) do
        let t1 = Test{}, t2 = Test{}

        // both of these are ok
        t1 = make in[r] int  
        t2 = make local int

        // this is not
        t1 = make local int

The compiler will use the lifetime of the field to determine whether or not the data structure can
be safely returned.  In the above code (assuming the last, erroneous line is elided), only `t1`
could be returned (were the function to return a `Test`).

Block references and arrays also have a similar rule: all elements must have the same lifetime
(if they are references).  Otherwise, the compiler can't ensure that the returned reference will
be safe.

## Value Categories

A **value category** is an aspect of any value that determines whether or not a reference
can be taken of it.  It is almost exclusively used for free references.  

1. L-Value -- has a well-defined place in memory, a free reference can point to it.
2. R-Value -- has a poorly-defined place in memory, a free reference can not point to it.

These are used elsewhere for things like copy ellision but are ultimately not that common.

## Constancy

All references can be denoted as constant by placing a `const` right before the
element type.  This means that the reference's value(s) cannot be mutated in 
any way.  The reference value itself can still be mutated unless it is declared as
a constant.  Block references with constant elements can still be resized.

Here are some example constant reference type labels:

    &const int
    own& const Test
    global [&]const bool

A reference can be allocated as const by placing a `const` after the region
specifier.  For example:

    make in[r] const (int, 5)
    new local const [1, 2, 3, 4]

Reference constancy cannot be cast or coerced away.  Additionally, a free reference
to a constant value must be constant.

    const x = 0
    let r = &x  // &const int

You can also take a constant free reference to a mutable value using the constant
reference operator `&const`.

## Global Memory

Global memory is memory allocated in the **global region**.  All the references we have looked
at thus far were local references meaning they belonged to a local region.  Global references
are prefixed by the keyword `global` and use a special allocation parameter also denoted
with the keyword `global`.

Global references are inherently unsafe and can be explicitly deleted using the `delete` function.
These references are always nullable and must check for nullable should they be used.  Global
references are also used for concurrency but should NEVER be accessed without a guard or lock
in concurrent situations.

Constructs at the global level may contain global references or other references: however, those
references are inherently nullable.  This practice is NOT recommended and should be avoided if
possible.

Global references may not be stored in local variables or constructs.

Global references should ONLY be used when absolutely necessary and when effective safety precautions
can be taken.

## Reference Operators

A **reference operator** is an operator that can be applied to a reference and act as it were
its original value.  These are often used to avoid unnecessary duplication on constructs that
employ copy semantics.  The `.`, `[]` and `[:]` operators all have reference forms.

## Copy Semantics

By default, references inside data types (such as block references) do not copy their contents
on assignment.  This is usually the desired behavior but not always.  If we want to add value
semantics to lists which are structs containing a block reference, we would need a way to force
a copy.

To do this in the general case, we implement the `Copyable<T>` interface which allows us to override
the default copying behavior on a type.  Instead of performing a normal value copy on a type,
the `copy` method will be called and whatever type is returned will be used as the copy.  Not
only does this allow us to force copying but it also allows us to avoid copying entirely.  

This method is called even the copy would normally be elided.  This can have a significant
performance impact if used incorrectly.

## Nullability

A reference can, like a value, be **nullable**.  Nullable references may or may not exist.  They
are uncommon and should be avoided.  The three cases for a nullable reference are: uninitialized
local references, global references, and local references stored globally. 

**Nullable operators** can be used on all references but are only required on global references.
Specifically, the **null-test operator** will accumulate a reference to its null value if it
does not exist.  It is denoted with a `?` placed after the reference.  This allows for tests such as:

    if ref? != null do
        ...

In that block (in the absence of a race condition/concurrency shenangans), `ref` is known to be
non-null.  In such blocks, global references can be accessed directly without nullable operators.

The null-test operator can be combined with any one of the reference operators to faciliate a
**nullable access operator** which will first perform a null-test before operating.  These
operators will accumulate to a null value if the null-test fails.

## Closures

Closures have peculiar memory semantics that need special attention -- especially since they do
not, in any way, accept region specifiers.

TODO: explain more how closures manage their state memory

