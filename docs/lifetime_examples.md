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
