# Shift-Reduce Conflicts

This file documents the known shift reduce conflicts in the language that
the parser auto-resolves as shifts.  These are situations in which the
parser chooses to blindly interpret code in a specific way because it
lacks information to make a more accurate judgement.  All of these conflicts
*could* be resolved by changing the language's syntax or using a more
sophisticated (and slower) parsing algorithm; however, such changes were
deemed more detrimental than helpful. 

## Heap Allocation

The single-type allocation specifier creates a shift-reduce conflict between
the angle bracket tokens used in vectors and generics and the less-than comparison
operator as well as the left-shift operator.

For example, the following syntax is invalid:

    make local int < r

Because the parser reads the `<` as an angle bracket beginning a vector data type
-- it expects a closing `>`.  Similarly, for generics:

    make local MyType < v

It reads the `<` as beginning a generic type list.

The solution to these kinds of situations is to simply wrap the heap allocation
in parentheses.  So for example, assuming the `<` operator is defined for `own &int`
and whatever `r` is, the following code accomplishes the task of applying a comparison

    (make local int) < r

Notably, since `<<` is not really a discrete token but rather a "compound token"
(special workaround for another grammatical issue), the same problems exist with it.

    make local MyType << v  // WRONG

    (make local MyType) << v // RIGHT

This problem is not fixed because it occurs so rarely in user code: the assumption
the compiler makes is almost always the correct one because references types do not
by default support comparison operators so a custom operator definition would be
required to render any of the above code semantically viable anyway.

An identical problem exists with `new` (collection initialization) as well.



