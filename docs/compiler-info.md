# Compiler Information

This file describes the general flow of the Whirlwind compiler and where to find
what.

## Table of Contents

- [Compilation Pipeline](#pipeline)
- [Package Legend](#legend)
- [HIR Optimizations](#hir-optim)
- [Common Fixes](#fixes)

## <a name="pipeline"/> Compilation Pipeline 

This section describes the general 5 stage compilation pipeline used for
Whirlwind.  Note that the first 3 stages are collectively referred to as
analysis and can fail if their required actions (such as parsing or symbol
resolution) fail. 

### Stage 1 - Initialization

The main package is initialized whereby all of the its files are parsed (if
possible) into abstract syntax trees and a `WhirlPackage` is created.  All
dependencies of the main package are likewise initialized recursively such that
all subsidiary dependencies are initialized. This process results in the
processing of all file-level annotations and import and lift-export statements
as well as the construction of the (directed) dependency graph representing the
full, interlocked web of dependencies necessary to fully produce the output
binary.

### Stage 2 - Resolution

All top-level definitions are analyzed and converted into HIR-Nodes and Symbols.
All imports and top-level symbol usages are resolved (or deemed unresolvable in
which case an error is thrown).  This results in all packages in the dependency
graph being primed for validation.

### Stage 3 - Validation

All predicates and blocks of the various top-level constructs are analyzed for
validity.  Type checking, local symbol resolution, memory analysis, constancy
and value category validation, and other phases of checking/validation are
performed at this stage.  The output from this stage should be a fully,
semantically-valid HIR of the source.  The majority of analysis takes place in
this stage.

### Stage 4 - Optimization

The HIR is transformed using a number of standard transformations to produce a
more optimized tree.  Transformations include various simplifications and
optimizations for common (inefficient) usage patterns.  This stage is intended
to work in tandem with the LLVM optimizer and so does not perform more standard
optimizations such as the removal of loop-invariant computation as these
operations will be performed by the LLVM optimizer (as necessary).

### Stage 5 - Generation

The optimized HIR tree (and associated packages) are transformed into LLVM
modules (and LLVM IR is produced from the HIR).  These modules are then
converted to optimized assembly (using the LLVM compiler) and are then assembled
and linked using standard tools.

## <a name="legend"/> Package Legend

This section describes the layout of packages (ie. where to find what)

| Package | Purpose |
| ------- | ------- |
| build | Acts as compiler's "main"/control package (stage #1) |
| common | Stores many common definitions (to prevent circular imports) |
| cmd | Responsible for command-line processing and most auxilliary functions of `whirl` |
| generate | Produces target LLVM code/output and assembles output binary (stage #5) |
| logging | Used to log warnings and errors throughout compilation and maintain context |
| optimize | Performs high-level (HIR) tree optimizations (stage #4) |
| resolve | Responsible for symbol resolution and package assembly (stage #2) |
| syntax | Scans and parses files to produce syntactically valid ASTs (stage #1) |
| typing | Defines the type system and facilitates type checking (stage #3) |
| walk | Converts an AST into a semantically-valid, unoptimized HIR tree  (stage #3) |

## <a name="hir-optim"> HIR Optimizations

### Range Optimizations

### Implicit Constancy

### First-Class Function Reduction

### Implicit Vectorization

## <a name="fixes"/> Common Fixes

This is a list of common issues/things to remember when developing on the compiler.

### Grammar Not Updating? 

Make sure you set the flag `forcegrebuild` when calling the compiler or delete the already existing PTable.
