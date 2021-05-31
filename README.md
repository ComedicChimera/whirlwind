# Whirlwind

**HEY: Whirlwind has moved -> Check it out [https://github.com/ComedicChimera/chai](here)**

Whirlwind is a compiled, modern, and multipurpose language designed with intentionality.
It is strongly-typed, versatile, expressive, concurrent, and relatively easy to learn.
It boasts numerous new and old features and is designed to represent the needs of any software developer.

***Language IP and Source Code Copyright &copy; Jordan Gaines 2018-2021***

***NOTE: This language is still in development! This is the third version of the compiler and language.***

### Related Repositories:

- [Whirlwind Docs Site](https://github.com/ComedicChimera/whirlwind-lang.dev/)
- [Whirlwind Packages Site](https://github.com/ComedicChimera/packages.whirlwind-lang.dev/)

## Table of Contents

- [Goals](#goals)
- [Notable Features](#features)
- [Feature Demos](#demos)
- [Examples](#examples)
- [Documentation](#docs)
- [Development Workflow](#workflow)
- [Contributing](#contributing)
- [Author's Note](#note)

## <a name="goals"/> Goals

Whirlwind has several main goals all of which can be enscapsulated in the idea of speed.

- Speed of writing.
- Speed of thinking.
- Speed of compiling.
- Speed of running.

Whirlwind tries to achieve all of these goals, and admittedly falls short in some areas.  When using more high level constructs,
there is often a trade off between ease of thought and writing and speed of compilation and execution.  When designing this
language, I aimed for an "85% solution" which effectively means close enough but not perfect: each construct fulfills the majority of our speed goals but not all at once.  In essence, instead of trying to make each and every construct perfect, I provide a variety of constructs and approaches each of which is suited to one or more of the goals, this design allowing you to choose what you find most important.

## <a name="features"/> Notable Features

- Versatile Type System
- Baked-In Concurrency
- Builtin Collections (arrays, lists, dictionaries)
- Pattern Matching
- Baked-In Vectors (and SIMD)
- Interface Binding
- Flexible Generics
- First Class Functions
- Powerful, Logical Type Inference
- Functional Programming Features
- Simple, Flexible Package System

*Note: This is not a complete list.  Also, many of the more unique features have additional information about them below.*

## <a name="demos"/> Feature Demos

***TODO: Insert some demos and explanations!***

## <a name="examples"/> Examples

***TODO: Add some more examples!***

Fibonacci:

    import println from io::std

    func fib() func()(int) do
        let a = 0, b = 1        

        return || do
            yield a

            a, b = b, a + b

    func main() do
        let f = fib()

        // prints first 10 fibonacci numbers
        for _ in 1..10 do
            println(f())

        f = fib()
        f() // 0

Radix Sort:

    import println from io::std

    func radix_sort(list: [int]) [int] do
        let mx = list.max()

        while let it = 0; 10 ** it < mx do
            let buckets = [make [int] for _ in 0..10]

            for item in list do
                buckets[item // (10 ** it) % 10].push(item)            

            list = buckets.flatten().to_list()
            it++

        return list

    func main() do
        let list = [9, 4, 7, 8, 2, 3, 9, 0, 0, 1]

        list = radix_sort(list)

        println(list) // [0, 0, 1, 2, 3, 4, 7, 8, 9, 9]

Linked List:

    import println from io::std

    type LLNode {
        value: int
        next: Option<&LLNode>
    }

    func ll_range(val: int) &LLNode do
        if val == 0 do
            return &LLNode{value=val, next=None}

        return &LLNode{value=val, next=Some(ll_range(val - 1))}

    func main() do
        let ll = ll_range(10)

        while true do
            println(ll.value)

            if ll.next match Some(v) do
                ll = v
            else
                break

## <a name="docs"/> Documentation

The language currently does not have a website or any other source of formal documentation as it is
still very much in development.  However, there is some documentation pertaining to the compiler's
design, the memory model, and other more significant aspects of the language and/or changes from
previous versions stored in the [docs](docs/) directory.  There is no table of contents or other
formal arrangement of these various documents.  Most of them are either Markdown files or plain text.
There are named in accordance with what they document.

There is a plan to create a more formal documentation (and ideally a website) as well as a full
start-up guide.  This README will be updated when that process is finalized.

# <a name="workflow"> Development Workflow

There is no formal schedule to the development of this language -- it has been very much on and off as
I also have to content with schoolwork, college apps, and general fatigue from working on this project
so long.  There may be large swaths of time in which no real progress is made.  I have not abandoned the
project -- I am simply taking a break for one of the reasons enumerated above and/or because something else
in my life has taken precedence.  It is also worth noting that although there may be no commits to this
repository, there may still be development going on.  Designing a language takes a lot more than just brute
coding -- I spend a lot of time just toying with different ideas and thinking about design approaches.

When I have more time, I may create a formal Trello to place here so you can see what I am currently working on.
I expect that as college apps start to fade out and my senior year starts to wind down, I will have more time
to work.  Building a language can be very tiresome, and there are days on which I may have some free time but
may simply be too tired or distracted to really sit down and work.  I hope this will be less of a problem as
time goes on but that remains to be seen.

Finally, as the note in header indicates, this language is subject to change.  I may spend weeks, months, or
longer committed to an idea or a specific direction for the language only to decide later that the direction
just doesn't align with what my vision for the language is. Language design is an iterative process, and this
language is ever-evolving much as I, its creator, am an ever-changing, growing person.  I realize this may
dissappoint some of you, but ultimately, I created this language for me and hoped that others might share my
desires and point of view on what  a language should be or what type of language they needed at that time --
I realize that "others" doesn't include everyone.

## <a name="contributing"/> Contributing

***TODO: Insert links***

Information about the compiler and all of its components can be found [here](docs/compiler-info.md).

If you would like to contribute to this compiler or this language, please review the information above
and join our Discord and other social media to get up to speed on the language in its current state.

- [Discord](insert link here)
- [Slack](insert link here)
- [Reddit](insert link here)

## <a name="note"/> Author's Note

This compiler is the realization of my vision as a programmer. I have been obsessed with languages
and language design pretty much since the first day I picked up programming. I hope that this language
can be as amazing to you as it is to me. I am always happy to have people who want to make
suggestions and contributions to help make this language as great as it can be.

By the time it is finished, this language will likely be the culmination of thousands of hours of work,
and I sincerely believe it will be worth it.
