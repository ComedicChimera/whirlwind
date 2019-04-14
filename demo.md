This file provides a basic overview of Whirlwind, its syntax, and its constructs.  It is by no means anywhere near a complete documentation
of the language (that can be found on the currently unfinished website).  This will just give you a brief look at the language.

## Syntax

Whirlwind is syntactically similar to several languages, but it is not completely like any of them.
For example, a function in Whirlwind will look like almost identical to Golang, but a variable declaration
has more in common with TypeScript.

Whirlwind has a lot of different, but linked features all of which will have a somewhat unique but shared syntax, so there
is really no way of giving you a full look at this syntax in totality.  This is also somewhat as a result of Whirlwind's
blended feature set, which includes aspects of functional, imperative, and object oriented languages, while not being completely
like any of them.

To give an example, suppose you wanted a program that would take in 10 numbers and return their average.  Here is one
way of doing that:

    include { Println, Scanf } from io::std;
    
    func main() {
        let nums: list[int];
        
        let input: int;
        for (_ <- 1..10) {
            Scanf("%d\n", ref input);
            
            nums.push(input);
        }
        
        let average = nums.foldl(|a, b| => a + b) / nums.len();
        
        Println("The average is:", average);
    }
    
As you can see, there are several different approaches the any given problem in Whirlwind and several different possible styles.
For example, in the beginning of the above program, we use a very imperative approach, but to calculate the average we employ a much
more functional style.  This is merely a thing of preference.

## Basic Constructs

## Efficiency
