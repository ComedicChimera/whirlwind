# Whirlwind

Whirlwind is a compiled, modern, and multipurpose language designed with intentionality.
It is strongly-typed, versatile, expressive, concurrent, and relatively easy to learn.
It boasts numerous new and old features and is designed to represent the needs of any software developer.

*Language and Source Code Copyright &copy; Jordan Gaines 2019*

### Goals:

Whirlwind has several main goals all of which can be enscapsulated in the idea of speed.

 * Speed of writing.
 * Speed of thinking.
 * Speed of compiling.
 * Speed of running.
 
Whirlwind tries to achieve all of these goals, and admittedly falls short in some areas.  When using more high level constructs,
there is often a trade off between ease of thought and writing and speed of compilation and execution.  When designing this
language I aimed for an "85% solution" which effectively means close enough, but not perfect: each construct fulfills the majority of our speed goals, but not all at once.  In essence, instead of trying to make each and every construct perfect, I provide a variety of constructs and approaches each of which is suited to one of the goals, this design allowing you to choose what you find most important.

### Notable Features:

- Interface Binding
- Generics
- Concurrent Functions (Asyncs)
- Lambdas and Closures
- Comprehensions
- Algebraic Data Types
- Dynamic Pointer Types
- Chaining (Complex Expressions)
- Inline Case Statement
- Captures
- Shadowing
- Constexpr Evaluation
- Builtin List and Dictionary Types
- Decorators
- Higher Order Functions
- Default Interface Members
- Composition and Binding Operators
- Operator Overloading
- Exponentiation Operator
- Packages

Other features that are less unique to Whirlwind include interfaces, structs, functions, variables, and all the other usual suspects.

### Syntax:

Whirlwind is designed to have a smooth, expressive, and concise syntax.  It is akin to a hodgepodge of various different styles, but they are all share similar patterns and styles.

A FizzBuzz function in Whirlwind might look like the following:

    include { Println } from io::std;
    
    func fizzBuzz() {
        for (i = 0; i < 100; i++) {
            if (i % 3 == 0 && i % 5 == 0)
                Println("FizzBuzz");
            elif (i % 3 == 0)
                Println("Fizz");
            elif (i % 5 == 0)
                Println("Buzz");
            else
                Println(i);
        }
    }
    
This is a very imperative approach to FizzBuzz, and there are innumerable other ways to approach this problem.

Whirlwind's blended syntax also propagates to the higher level language constructs.  For example,

    struct Vector3 {
        x, y, z: int;
    }
    
Again, we can see the concise nature of Whirlwind's syntax.  Whirlwind only makes your write what is necessary and this can
be seen everywhere in the language.

Whirlwind also features pattern matching in a number of situations that can prove very useful:

    func main() {
       let t = (7, "abc", 3.14);
       
       // tuple pattern matching
       let (x, y, _) = t;
       
       // case pattern matching
       let res = t case {
          (_, "abc", let z) => z,
          (4, _, _) => 1,
          _ => 0
       };
       
       let a: any = 56;
       
       // type pattern matching
       if (a is i: int) {
           // do something with i
       }
   }
   
In summary, one way Whirlwind encourages speed of development is by providing a concise, but expressive syntax.

### Resources:

You can learn more about Whirlwind and its design on *whirlwind-lang.org*.  This site provides a formal language specification, a
basic guide to the language, and many other useful tidbits of information and functionality.

If you are looking for a more personal and interactive experience, you can look at our social media links down [below](#compiler-info).

## Compiler Information and Contributing <a name="compiler-info">

Information about the compiler and all of its
components can be found [here](https://github.com/ComedicChimera/Whirlwind/blob/master/Whirlwind/docs/compiler_info.md).

A comprehensive language documentation can be found [here](website/docs) *insert link*

If you would like to contribute to this compiler or this language, please review the information above
and join our Discord and other social media to get up to speed on the language in its current state.

 - [Discord](insert link here) *insert link*
 - [Slack](insert link here) *insert link*
 - [Reddit](insert link here) *insert link*
 
*Please read the full reference provided above before contributing the
repository.*

## Author's Note
This compiler is the realization of my vision as a programmer. I have been obsessed with languages
and language design pretty much since the first day I picked up programming. I hope that this language
can be as amazing to you as it is to me. I am always happy to have people who want to make
suggestions and contributions to help make this language as great as it can be.

By the time it is finished, this language will likely be the culminations of thousands of hours of work
and I sincerely believe it will be worth it.


