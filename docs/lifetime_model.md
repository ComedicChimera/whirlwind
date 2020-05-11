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
| Global | ❌ | ✔️ | ✔️ |
| Duplicate | ✔️ | ❌ | ❌ |

*Note: Polymorphic lifetime variables have different semantics depending on what lifetime is evaluated (eg. in each branch of an `if`)*

*Note: `=` means direct assignment to the owner not mutation of the value it stores (eg. `*x = 2` is acceptable even when `=` is not).*

*Note: Not all usages in the above table are entirely axiomatic (lifetime polymorphism shakes things up a bit).*

As should be clear from this table, the usages for basic lifetimes are fairly straightforward.  They essentially enforce what the lifetime
promises about a resource.  For example, a local lifetime promises that a resource will be deleted when its enclosing scope closes.  Therefore,
it makes sense that raw assignment is not valid on it because that would lead to the original resource stored in the local owner not being
able to be deleted and thusly becoming inconsistent with the lifetime of its owner.

## Lifetime Polymorphism

In the real world, resources rarely have simple lifetimes.  For example, consider the following function that takes in a resource and either
returns a new value for the resource and deletes the old one or returns the original value of the resource.

    // R is the type of some large resource
    func get_res(own r: dyn* R) own dyn* R {
        if some_global_cond {
            delete r;
            return make R;
        }

        return r;
    }

Very obviously, something is wrong here. `r` does not have a consistent lifetime: it is either deleted (local lifetime) or returned as it was
(nonlocal lifetime).  Moreover, notice the `delete r` that occurs in an `if` block.   In a scope in which, `r` is nonlocal: `delete` shouldn't
be valid here and yet, if you compile this code, it will compile and work as intended.  What is going on here?

The function above is an example of lifetime polymorphism wherein, due to the control flow of your code, a resource can assume a number of
different lifetimes without violating the overall lifetime semantics of the language.  Let's look at the flow of the above code:

    some_global_cond:
    true -> resource is deleted before function returns
    false -> resource is not deleted before function returns

As we can see, the above flow is quite clear to the compiler: it knows what should happen on each branch (each state of `some_global_cond`) and
most importantly knows that each branch will not interfere with the semantics of the other branches.  These two circumstances allow for lifetime
polymorphism.  

Let's consider several other examples of where a compile error would occur and lifetime polymorphism would not be possible.  First, let's look
at an almost identical version of the same function but with one small omission.

    func get_res(own r: dyn* R) own dyn* R {
        if some_global_cond {
            // delete is gone
            return make R;
        }

        return r;
    }

In this case, the compiler raises an error.  Why?  Remember our first condition of validity: the compiler has to know what to do.  Here it doesn't.
Should `r` be deleted when the scope closes or should it be allowed to linger?  It could make a guess but as our situations get more and more
complicated such guessing becomes infeasible and moreover, unclear.  The `delete` is mandatory to confirm the compiler's suspicion of what should
happen on that particular branch.

Let's look at another example.

    func f() own dyn* int {
        let x = make int;

        if some_cond {
            delete x;
        }

        return x;
    }

Here we have a different problem: the semantics of a conditional branch are interfering with those of the larger branch.  The larger branch expects
`x` to be nonlocal when it receives it; however, our smaller branch may conditionally render it local instead which is a problem.  If the
`return x` were to replaced with some other return value that didn't involve `x`, this usage would still be invalid because now `x` would
be nonlocal to the conditional scope which would cause lifetime inconsistency.

A final example is the following code:

    func g() own dyn* int {
        let r = make R;

        for i = 0; i < 10; i++ {
            if i > 7 {
                return r;
            }
        }

        return null;
    }

Looking at this, you and I both know that there is no real problem here: `r` is always nonlocal because `i` will always be greater than `7`.
The problem is though that the compiler doesn't know this and therefore doesn't know what the lifetime of `r` should be in the main branch.
There are two solutions to the problem, the first solution simply involves inserting a delete before the return like so:

    func g() own dyn* int {
        let r = make R;

        for i = 0; i < 10; i++ {
            if i > 7 {
                return r;
            }
        }

        delete r;
        return null;
    }

This is now acceptable because the lifetime semantics are now clear to the compiler: `r` is returned if our conditional branch runs (nonlocal) or
`r` is deleted if it doesn't (local).  However, you and I both know that the second condition will never really happen.  In fact, we both know
that the `return null` also never happens.  Luckily for you, Whirlwind has another solution:

    func g() own dyn* int {
        let r = make R;

        for i = 0; i < 10; i++ {
            if i > 7 {
                always return r;
            }
        }
    }

The `always` statement is called a claim: it is a promise to the compiler that a certain key statement will run.  If you compile the above code, it
will work because the compiler now sees only one possible branch: `r` is returned.  Note that claims can be very dangerous if used recklessly:
the compiler will assume that you are telling the truth which can lead to the really nasty consequences if you weren't.  In this case, we know
that our claim is true so we are fine.
