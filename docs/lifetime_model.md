# Lifetime Model

Whirlwind uses a "lifetime model" to help the programmer manage dynamic memory.  This model operates on two principles:

- Every resource has a lifetime that is either compiler-determined or user-specified (depending on circumstance).
- Every resource's lifetime must be **provably** consistent to that of its "owner".

This model allows Whirlwind to guarantee that withing the bounds of the model it is impossible to create a memory leak.
However, Whirlwind makes no such guarantee about memory integrity (that is memory can be variably null).  Instead Whirlwind
offers you ways to avoid undefined behavior: runtime panics and nullable operators.  The enforced principle is that unless
specified otherwise by an annotation, every dereference (of any form) can be either nullable or non-nullable.  In each case,
a null or invalid dynamic pointer is handled by coalescing to null or by causing a runtime panic respectively.  This system
prevents undefined behavior but does have a mild performance cost (due to checks for said invalid pointers) and so to allow
you to maximize Whirlwind's performance at the cost of safety, a file or function can be labeled as "unsafe" via an annotation
in which case all checks related to memory (and to other features of the language as well) are disabled.

## Basic Lifetime Characteristics

Whirlwind provides five different lifetime categories that can either be user-specified or compiler-determined.  It is important
to note that lifetimes appear relative to the scope in which they are enclosed (eg. a resource could have one kind of lifetime
in an upper scope but a different kind of lifetime in a lower scope).  The five kinds of lifetimes are as follows:

- Local
- Nonlocal
- Global
- Duplicate
- Polymorphic

Local lifetimes are the simplest.  A resource with a local lifetime is deleted whenever its enclosing scope closes.  For example,
in the following code `x` has a local lifetime.

    func f() {
        let x = make int;

        // -- SNIP --
    }

This means that `x` will be deleted when the function returns.  The next kind of lifetime is a bit more complex: the nonlocal lifetime.
Resources with a nonlocal lifetime have the opposite characteristics to those of the local lifetime: they are not deleted whenever the
current enclosing scope closes.  Note that this scope may not necessarily be their (ie. a resource can be local to "higher" scope and
nonlocal in a "lower" scope).

There are a number of ways a resource or owner can have a nonlocal lifetime.  The first an most obvious is if a resource is a created
in a higher scope.

    func f() {
        let x = make int;

        if some_cond {
            // -- SNIP --
            // x is nonlocal to the scope
        }
    }

However, a resource can also be elevated to a nonlocal lifetime.  The most common ways for this happen are function returns and assignment.
We will talk about the latter a bit later since it is a bit more complicated.  However, we can observe function return lifetime elevation
in action here.

    func f() own dyn* x {
        let x = make int;

        return x;
    }

In this context, `x` is elevated to having a nonlocal lifetime in its enclosing scope.  Note the use of the `own` keyword here.  We use this
keyword to indicate that we want Whirlwind to elevate the lifetime of `x` to be local to the scope in which `f` is called (this also means
an owner is expected to receive the value returned by `f`).  If we were to omit the `own` keyword, `x` would default to its normal lifetime
within `x` (ie. a local lifetime) and be deleted before it ever reached the caller.  Note that if `x` has been nonlocal (or global) to the
enclosing function, it would retain its original lifetime and also become nonlocal to the calling scope.  This can be useful for accessing
and managing shared resources.

The next two kinds of lifetimes are much simpler.  The first is the global lifetime.  Owners with a global lifetime are global to the
current package, and resources must be explicitly elevated to the global lifetime via assignment or movement.  Note that resources with
a global owner cannot be and are never deleted; however, they can be moved (as we will see later).

The fourth kind of lifetime is the duplicate lifetime which is essentially an owner with no lifetime at all.  This used whenever a copy
of a resource is stored in another named value.

Notably, dynamic function arguments are by default considered to have duplicate lifetimes (this is because they are copied before being passed in).

    func f(x: dyn* int) {
        // -- SNIP --
    }

The parameter `x` has a duplicate lifetime in this context.  We can also make a parameter local to a function by using the
`own` prefix as follows.

    func f(own x: dyn* int) {
        // -- SNIP --
    }

`x` will now be deleted when our function closes.

The final kind of lifetime will be explored in later sections as will the semantics for all of the lifetimes (enumerated or not).

## Lifetime Indeterminance

Sometimes it may not be possible for the compiler to clearly determine a lifetime for a resource.

    func g() own dyn* int {
        let x = make int;

        if some_cond {
            return x;
        }

        // ERROR!
        return null
    }

In this situation, `x` has an indeterminate lifetime.  What happens if the function doesn't return?  Should
`x` be deleted or linger?  While in this situation the desired may seem obvious, one of Whirlwind's goal is
expressivity and if the compiler were to delete our resource here implicitly, the behavior would not be obvious
to a reader and so Whirlwind requires you to explicitly delete the resource.

    func g() own dyn* int {
        let x = make int;

        if some_cond {
            return x;
        }

        delete x; // MUCH BETTER!
        return null;
    }

Another reason for this requirement is that it helps reveal to the programmer in unintentional bugs.  Imagine if
instead the programmer had meant to write:

    func g() own dyn* int {
        let x = make int;

        if some_cond {
            return x;
        }

        log("Some_cond wasn't true");
        delete x;
        return null;
    }

Now by simply noting an inconsistency in our variables lifetime, we have reminded the programmer to log that their condition
had failed (because such a log might be mentally associated with the idea of a "clean up" task).

The astute among you at this point have probably noticed something rather odd about our variable `x`.  It seems to have two
possible lifetimes: nonlocal and local.  What is going on here?  This is where the idea of the polymorphic lifetime fits in.
As is obvious the world is rarely simple enough for every resource to be described in only one way.  So Whirlwind allows for
polymorphic lifetimes to handle more complex situations like the one above.

## Making Claims

To allow its memory model to be more robust, Whirlwind allows to make "claims" about the behavior of your program.  Now,
these claims go way beyond just the memory model but we will limit ourselves to just their use there.  Let's consider
a simple example:

    func h() own dyn* int {
        let x = make int;

        for i = 0; i < 10; i++ {
            if test(i) {
                return x;
            }
        }

        // ERROR!
        return null;
    }

Ordinarily, you would be required to write `delete x;` at the end of the function.  However, let's say that you as a programmer
know that the `test` will return true on at least one `i` given to it.  While writing the delete is not necessarily that inconvenient,
it can be annoying when trying to compile your program for it to give you generally unhelpful errors and for you to have write a line
that adds nothing to your code.  Now, imagine if you could somehow communicate to the compiler your higher knowledge of the program.
Well, with claims you can.  Let's rewrite our above code using a claim.

    func h() own dyn* int {
        let x = make int;

        for i = 0; i < 10; i++ {
            if test(i) {
                always return x;
            }
        }

        // No error :)
    }

Now isn't that some much nicer!  We just dropped two lines there: the return and the delete because we told the compiler our sacred knowledge.
Now, Whirlwind knows that you are sometimes going to lie to it even if unintentionally and so will often generate some filler code to make sure
that you don't experience any undefined behavior from say, your function not returning but in the context of the memory model, it will not insert
an additional delete as you have told it that it doesn't need to (I hope you weren't lying!).

## Lifetime Consistency

Whirlwind requires that a resource's lifetime be able to be made consistent with the lifetime of its owner.  This fundamental principle is
how Whirlwind prevents memory leaks and has interesting ramifications for how Whirlwind code is written.  This rule also has an incredibly
important implication that is worth mentioning: a resource's lifetime must remain consistent with the lifetime of its owner.

So how do we realize these rules?  We begin by defining the usage of two keys operators (`=` and `:>`) and the `delete` statement. All
of these operators have the ability to change the lifetime of an owner's resource.  Before we get to theory, let's look at a simple
table to determine what operations are valid on each kind of lifetime.

| Lifetime | `=` | `:>` | `delete` |
| -------- | --- | ---- | -------- |
| Local | ❌ | ✔️ | ✔️ |
| Nonlocal | ❌ | ✔️ | ❌ |
| Global | ❌ | ✔️ | ❌ |
| Duplicate | ✔️ | ❌ | ❌ |

*Note: Polymorphic lifetime variables have different semantics depending on what lifetime is evaluated (eg. in each branch of an `if`)*

*Note: `=` means direct assignment to the owner not mutation of the value it stores (eg. `*x = 2` is acceptable even when `=` is not).*

*Note: Not all usages in the above table are entirely axiomatic (lifetime polymorphism shakes things up a bit).*

As should be clear from this table, the usages for basic lifetimes are fairly straightforward.
