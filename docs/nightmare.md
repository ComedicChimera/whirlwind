# The Generic and Opaque Nightmare

**Warning**: Do not read this file unless absolutely necessary.  Designing this
system cost me a good bit of sanity and definitely some small piece of my soul.
Read at your own risk.

This file describes how the compiler handles generic and opaque types in great
detail.  It covers all the ridiculous edgecases and the *six* (yes, I sh!t you
not, six!) "types" used to make this whole system work.  Note that type here
is meant in the sense of anything that implements the `typing.DataType` interface
not an actual type in the broader context of the language.

## Opaque Types

An `OpaqueType` is used to handle cyclic dependencies.  Effectively, `OpaqueType`
is defined as a placeholder for some other type in a definition cycle -- it is
later filled in later with the actual type once the cycle has been resolved.

For example, consider the following cycle:

    type A {
        field0: B
        field1: string
    }

    type B {
        field2: C
        field3: int
    }

    type C
        | Var1(int)
        | Var2(A)

All three types depend on each other cyclically.  To resolve this, the compiler
picks on of the three types to be declare as an `OpaqueType`.  WLOG let's consider
`A` is our `OpaqueType`.  Once this declaration happens, `C` can resolve since now
`A` is defined which then causes `B` to resolve.  Once these two types have finished
resolving, we can now resolve `A` since `B` and `C` are now resolved.  We fill in
the `EvalType` field of `OpaqueType` with that newly defined type `A` and since all
the other types store a reference to an `OpaqueType`, the actual value of `A` will be
updated in them thereby allowing us to create a mutual dependency relationship without
cause infinite recursion during compilation.

As for requiring references to cyclically defined types, the compiler can't always tell
when one is actually necessary, and it knows one isn't always required.  So, it doesn't
always throw an error if a reference is not used even if one might actually be required.
Instead it relies on the fact, that, in many cases, it would actually be impossible for
the user to write correct code without using a reference -- they would end up with an
infinite chain of required instantiations and so their code would never compile for another
reason. 

*Note: This may get changed based on how LLVM reacts but that remains to be seen*.

## Simple Generic Types

## Opaque Generic Types

## Generic Algebraic Types

## Algebraic Variant Erasure

The `AlgebraicVariant` type and its companion `GenericAlgebraicVariantType` are
never used in actual expressions -- they are replace with their parent as soon as
they are created meaning all `AlgebraicVariant` types only store `AlgebraicType`
as parents.  This change simplifies coercion -- the original variant type is made clear
in the expression that constructs the algebraic variant.





