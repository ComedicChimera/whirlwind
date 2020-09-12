package build

import (
	"github.com/ComedicChimera/whirlwind/src/common"
)

/*
IMPORT ALGORITHM
----------------
1. Construct a directed, cyclic graph of all of the packages required to build
   the root package with their associated dependencies. All packages should be
   initialized in this stage.

2. For each package in the graph, determine if that package is in a cycle with
   another package.  If so, cross-resolve the symbols of the packages across the
   entire cycle (by considering their global tables spliced).  Otherwise,
   cross-resolve the symbols of the current package exclusively.

   a) By cross-resolve, we mean resolve the symbols nonlinearly such that
   definitions that are provided out of proper order can be reorganized sensibly
   without having to late-resolve any symbols.

   b) By splicing the tables of mutually dependent packages, we mean perform
   cross-resolution as if the packages were sharing a symbol table although they
   are not ultimately.  Note that this method of resolution does not corrupt the
   integrity of any package's namespace as it is being resolved.

   c) All unresolved or unresolvable symbols should be identified in this stage.

   d) Every symbol resolved implies that an satisfactory definition was found for
   it and said definition has undergone top-level analysis (so as to produce as
   a top-level HIR node).

3. Extract and evaluate all the predicates of the top-level definitions.
   Subsequently, analyze any generates produced.  After this phase, the package
   is said to "imported".  However, it has yet to be "built" (-- "compiled").

   a) Final target code generation will be accomplished at a later stage (this
   is what is referred to by "built").

   b) This algorithm's completion also connotes the full completion of analysis.
*/

// initDependencies extracts, parses, and evaluates all the imports of an
// already initialized package (performing step 1 of the Import Algorithm)
func (c *Compiler) initDependencies(pkg *common.WhirlPackage) bool {
	return true
}
