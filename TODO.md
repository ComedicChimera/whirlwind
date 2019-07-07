# TODO

- change self referential types to work off a prefixed name system and remove self-type
- add package linker
- add 2 sanitizers (constexpr and memory) [Note: constexpr does in depth checking on all constant expressions]
- remove MirrorType and declare builtins in SymbolTable as necessary
- add prelude (later)
- allow for context-based inferencing (for lambdas and enumerated type classes)
- clear up the behavior of null
- update type class to use guards on the type constructor as opposed to having
a separate (and illogical) value restrictors
  * type Type Val(v: Int) when v < 3;
- make sure type classes can only allow one alias (for logic purposes)
and make sure that unpacking is functional in its more complex state (as in the
following works as intended)
  * from docs: `let num = Number::Int(3); let t: int = from (num as Int);`
- allow decorators to work with function groups
- add range operator overload
- add static as a life time specifier (to supplement the behavior lost by closures)
- add pattern matching on variables after `is`
  * `x is Type t`
- add context based inferencing to type classes instead of declaring in global scope
and make sure constancy works
  * see docs on Type Classes
- fix references to have a more logical behavior
- add special binding syntax to allow for binding onto all types of pointers
- make sure finalizers work as intended

# THOUGHTS

- add positioning data to some or all of the action tree (maybe identifiers?)
- make sure to properly name arg and parameter variables
- change integer literals to default to signed integers and make signed-unsigned coercion easier
- add annotations to describe memory and program behavior (using `#` syntax)
- add string interpolation
- find a way to vary behavior based on type for interfaces (interface level method variance, a buffed is operator, etc.)
  * although it is possible to do this via overloading
- rework casting syntax to be more friendly

# FUTURE

- add operator overloading during generation
- add strict group overload matching during compilation
- make sure compiled code handles coercion properly (particularly on tuples)
- make sure closures obey the behavior described in the docs
  * they share their state, don't copy it
- make sure that null initialization is pervasive
  * this should work: `let x = f(); func f() int => x;`
- make sure to account for out of order variable declaration if necessary

# TESTING

- static get
- change include syntax to use `::` instead of `.`
  * include { Println, Scan } from io::std;
  * include ..a::b;

