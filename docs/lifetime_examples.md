# Lifetime Examples

This file showcases some example situations with lifetimes in Whirlwind.

## Simple Lifetimes

Current-Frame Lifetime

    func f() {
        let x = make int;

        for i = 0; i < 10; i++ {
            // loop stuff
        }

        g();

        // x implicitly deleted here
    }

Elevated Lifetime (Returning)

    func f2() dyn* int {
        let x = make int;

        // stuff happens, yada

        return x; // x's lifetime is elevated to the caller
    }

Elevated Lifetime (Nonlocal)

    func f3() {
        let x = make int;

        // -- SNIP --

        global_var = x; // x's lifetime now that of enclosing/global variable
    }

Argument Lifetime (standard)

    func f4(p: dyn* int) {
        // function logic

        // p NOT deleted here
    }

Argument Lifetime (owned)

    func f5(own p: dyn* int) {
        // function logic

        // p deleted here
    }

## Conditional Lifetimes (I)

Example Situation

    func h() dyn* int {
        let x = make int;

        if some_cond {
            return x;
        } else {
            // something else
        }

        // ERROR: x has indeterminate lifetime (could be deleted or could linger)
    }

Three Ways to Resolve This:

    // CONSISTENCY
    func h() dyn* int {
        let x = make int;

        if some_cond {
            // -- SNIP --
            return x;
        } else {
            // -- SNIP
            return x
        }

        // No error: x has determinate lifetime
    }

    // DELETION
    func h() dyn* int {
        let x = make int;

        if some_cond {
            return x;
        } else {
            delete x;
        }

        // No error: x has polyformic but determinate lifetime
    }

    // CLAIM
    func h() dyn* int {
        let x = make int;

        if some_cond {
            always return x;       
        } else {
            // -- SNIP --
        }

        // Error: else is now deadcode b/c the compiler knows that the if branch always runs
        // but memory/lifetime error has been resolved
    }

## Conditional Lifetimes (II)

Example with loops

    func l() dyn* int {
        let x = make int;

        loop some_cond {
            return x;
        }

        return null;
        // Error: x has indeterminate lifetime
    }

Three solutions once again

    // CONSISTENCY
    func l() dyn* int {
        let x = make int;

        loop some_cond {
            return x;
        }

        return x;
    }

    // DELETION
    func l() dyn* int {
        let x = make int;

        loop some_cond {
            return x;
        }

        delete x;
        return null;
    }

    // CLAIM
    func l() dyn* int {
        let x = make int;

        loop some_cond {
            always return x;
        }

        return null; // Error: deadcode
    }

Note: This example is fairly contrived, but imagine that the loop did some logic and ran multiple times but always returned
eventually.  In that situation, `always` would be quite useful!

Note: "Collective" lifetimes work the same way as regular lifetimes (ie. if you have some type that stores a dynamic pointer,
that pointer's lifetime is tied to the lifetime of the data structure in the same way as it would be if it were a variable).
However, if you have something like a list that is resizable, the compiler will "cache" the dynamic pointer before it is stored
to delete it at the end of the stack frame (or conditional block).

Note: As hinted at before, lifetimes are tied to blocks not necessarily stack frames.  So if you allocate a resource in an if block,
it has the same semantics as if that if block were a function block (eg. assigning upward still leads to a nonlocal lifetime).

## Assignment Lifetime Violations

The assignment operator may not be valid on a dynamic pointer in certain contexts.  For example,

    func p() {
        let x = make int; 

        loop {
            x = make int; // Error: lifetime violation
        }
    }

Lifetime violation occur when the compiler is unable to enforce the determined lifetime of a resource holder on the resources it
holds.  In this case, the newly created integers cannot be cached and deleted at the proper end of their lifetime and so a violation
occurs.  Another example:

    func p(x: own dyn* int) {
        if some_cond {
            x = make int; // Error: lifetime violation
        } else {
            delete x;
        }
    }

The compiler once again cannot cache and delete in the same scope here.  Although the compiler could theoretically raise the allocation out
of the `if` block, that would violate the expected program flow (int is only allocated if some_cond is true whereas with caching it would have
to be always allocated).  This is also unacceptable:

    func p(x: dyn* int) {
        if some_cond {
            x = make int; // Error: inconsistent lifetime
        }

        // etc.
    }

Because we don't know whether or not `x` should be deleted at the end of the function (the lifetime of the newly made resource is different from the
lifetime of the resource that is passed in).  On the other hand, this is acceptable:

    func p(x: dyn* int) {
        // -- SNIP --

        x = make int;

        // -- SNIP --
    }

The reason is that the lifetime of unspecified parameter is determined to be that of the resources assigned to it.  By default, the lifetime is equal to
that of the resource passed in.  But, because Whirlwind knows that the lifetime of outer resource is not managed by the function, and it knows that the lifetime
of the variable changes deterministically, it is able to delete the resource safely without violating the lifetime of the resource passed in.

Note: In all of these situations, `*x = some_value` would be valid because it does not effect the resource's lifetime.

Note: The move operator would be valid in all but third situation (where it could lead to an inconsistent lifetime for the outer resource).

## Move Validity

In the following two examples, we see situations where move is valid and where it isn't.

    func q() {
        let x = make int;

        if some_cond {
            x :> make int; // OK: lifetime of outer resource holder is consistent
        }
    }

    func q2(p: dyn* int) {
        p :> make int; // Error: inconsistent lifetime
    }

The reason the second example is inconsistent is that although it is consistent with the lifetime of parameter (as a resource holder), it is not consistent with
the lifetime of the resource itself (the function could be "deleting" a resource unexpectedly)

## Volatile Pointers

Volatile pointers are free of any lifetime semantics and are type ambivalent (both a stack and heap pointer can be a volatile pointer).  This means all memory
operators are valid on them unconditionally, but it also means that they are VERY dangerous and should be used with caution.
