# Removals

This enumerates some of the features that were removed, reworked or
replaced from previous versions of the language.

- `->` operator, just use `.` for everything
- constructors (use init-lists)
- group overloading
- old `match` statement (ie. `match<...> expr to ...`)
- `select` keyword => replaced by match
- pattern matching on `is`
  - moved into `match` logic
- `from ... as` syntax
  - just use pattern matching
- special methods: not necessary anymore
  - hide functionality
- expression local bindings
  - no expression local variables or expression local chaining
  - all symbols declared in expressions are declared in enclosing
  scope
  - cleaner, move obvious, less bad code
- old partial function syntax
  - `|f ...)` just looks noisy
  - still a feature, done much more logically
    - eg. `f(_, 2)`
    - see **Syntactic Sugar and Smaller Adjustments** for details
- the `default` keyword
  - `case _ do` is just as clear
  - inline with rest of language (inc. inline `match`)
- C-Style for loop
  - accomplishable (basically) with iterators and while loops
  - just looks ugly
- implicit infinite loop
  - just kinda looks ugly
  - not necessarily unclear but really not very idiomatic