package syntax

import (
	"reflect"
	"sort"
)

const _goalSymbol = "top_level"

// LRItem represents an LR(1) item (concisely)
// 24B on a 64 bit machine
type LRItem struct {
	// Rule refers to the rule number in the RuleTable not
	// the final rule number in the parsing table
	Rule int

	// DotPos refers to the index the dot is considered to
	// be placed BEFORE (so a dot at the end of the item
	// would have a dot pos == to the length of the rule)
	DotPos int

	// Lookaheads stores the terminal values of the lookaheads
	Lookaheads []int
}

// LRItemSet is used to store the actual slice of items as well as the sets
// connections to other itemsets (nonterminal and terminal) - by index
type LRItemSet struct {
	Items []*LRItem

	TerminalConns    map[int]int
	NonterminalConns map[string]int
}

// PTableBuilder holds the state used to construct the parsing table
type PTableBuilder struct {
	BNFRules *RuleTable
	Table    *ParsingTable

	ItemSets []*LRItemSet

	// allows us to memoize first sets by nonterminals
	firstSets map[string][]int
}

// constructParsingTable takes in an input rule table and attempt to build a
// parsing table for it.  If it fails, a descriptive error is returned.  If it
// succeeds a full parsing table is returned.  NOTE: this function resolves
// shift-reduce conflicts in favor of SHIFT (will warn if it does this)!
func constructParsingTable(rt *RuleTable) *ParsingTable {
	ptableBuilder := PTableBuilder{BNFRules: rt, firstSets: make(map[string][]int)}

	ptableBuilder.Build()

	return ptableBuilder.Table
}

// Build uses the current builds a full parsing table for a given rule table
func (ptb *PTableBuilder) Build() {
	// augment the rule set
	augRuleNdx := len(ptb.BNFRules.Rules)
	ptb.BNFRules.Rules = append(ptb.BNFRules.Rules,
		&Rule{ProdName: "_END_", Contents: []interface{}{BNFNonterminal(_goalSymbol), BNFTerminal(EOF)}})
	ptb.BNFRules.ProdRules["_END_"] = []int{augRuleNdx}

	// create the starting kernel
	startKernel := &LRItemSet{Items: []*LRItem{&LRItem{Rule: augRuleNdx, DotPos: 0, Lookaheads: []int{EOF}}}}

	// calculate its closure to get the starting set
	startSet := ptb.closureOf(startKernel)

	// add that set to the list item sets (so it can be recognized in nextSets)
	ptb.ItemSets = append(ptb.ItemSets, startSet)

	// calculate the next sets from the starting item set
	ptb.nextSets(startSet)
}

// nextSets takes in a starting set; initializes its connections, and computes
// all valid sets that could follow it (recursively).  It creates appropriate
// connections for all sets in the item graph (including handling repeats). It
// expects its starting set to already be added to the known item sets.
func (ptb *PTableBuilder) nextSets(startSet *LRItemSet) {
	// assume that the connections are uninitialized
	startSet.TerminalConns = make(map[int]int)
	startSet.NonterminalConns = make(map[string]int)

	for _, item := range startSet.Items {
		ruleContents := ptb.BNFRules.Rules[item.Rule].Contents

		// if the dot is at the end of the rule or there is an epsilon in the
		// rule (which implies due to the construction of our rule table that it
		// is an epsilon rule), then there are no further sets to create
		if item.DotPos == len(ruleContents) {
			return
		}

		dottedElem := ruleContents[item.DotPos]

		switch v := dottedElem.(type) {
		case BNFEpsilon:
			// following logic explained in prior comment
			return
		// next two cases check for repeats (avoid recalculation)
		case BNFNonterminal:
			if _, ok := startSet.NonterminalConns[string(v)]; ok {
				continue
			}
		case BNFTerminal:
			if _, ok := startSet.TerminalConns[int(v)]; ok {
				continue
			}
		}

		// otherwise, compute the GOTO of the item set and determine whether or
		// not to add it to graph and whether or not to connection (recall: all
		// constraints of `gotoOf` have already been handled)
		gotoSet := ptb.gotoOf(startSet, dottedElem)

		gotoMatched := false
		for i, set := range ptb.ItemSets {
			if set.equals(gotoSet) {
				// if we have already calculated the state, create a connection
				// back to that precreated state, set the matched flag, and exit
				// the loop (b/c we don't need to search anymore)
				gotoMatched = true
				startSet.addConnection(dottedElem, i)
				break
			}
		}

		// if we have matched, then the connection is already added as is the
		// state so we don't need to add either. (we can skip this last stage)
		if !gotoMatched {
			// add the connection to our goto set (predictively)
			startSet.addConnection(dottedElem, len(ptb.ItemSets))

			// add the goto set to our graph
			ptb.ItemSets = append(ptb.ItemSets, gotoSet)

			// recursively progress to the next state
			ptb.nextSets(gotoSet)
		}
	}
}

// gotoOf calculates the next state of a given item set when provided with a
// given input (NOT EPSILON).  NOTE: it should not be called on an item set
// where the dot is at the end of any item (should be handled elsewhere).
func (ptb *PTableBuilder) gotoOf(itemSet *LRItemSet, element interface{}) *LRItemSet {
	newItemSet := &LRItemSet{}

	for _, item := range itemSet.Items {
		ruleContents := ptb.BNFRules.Rules[item.Rule].Contents

		// only calculate goto if the element matches
		if reflect.DeepEqual(ruleContents[item.DotPos], element) {
			newDotPos := item.DotPos

			// move the dot forward unconditionally
			newDotPos++

			// the lookaheads of the original items with dots moved remain the
			// same (only changes when calculating closure) - as does rule
			newItem := &LRItem{Rule: item.Rule, DotPos: newDotPos, Lookaheads: item.Lookaheads}

			// if the dot is now at the end of the rule, we don't need to
			// calculate closure at all (and in fact, can't)
			if newDotPos == len(ruleContents) {
				newItemSet.addToSet(newItem)
			} else {
				// we need to calculate the closure so we create a new item set
				// to close over and then combine it with the original item set
				gotoKernel := &LRItemSet{Items: []*LRItem{newItem}}

				// no need to reorder the set before closure (still ordered)
				newItemSet = union(newItemSet, ptb.closureOf(gotoKernel))
			}
		}
	}

	// also implicitly ordered by union and addToSet (as the sets are built up)
	return newItemSet
}

// closureOf calculates the closure of the kernel of an item set NOTE: should
// NOT be called on a kernel where the dot is at the end of any item
func (ptb *PTableBuilder) closureOf(kernel *LRItemSet) *LRItemSet {
	for _, item := range kernel.Items {
		ruleContents := ptb.BNFRules.Rules[item.Rule].Contents

		if nt, ok := ruleContents[item.DotPos].(BNFNonterminal); ok {
			var lookaheads []int

			if item.DotPos == len(ruleContents)-1 {
				lookaheads = item.Lookaheads
			} else {
				lookaheads = ptb.first(ruleContents[item.DotPos+1:])

				// firsts are not necessarily ordered so we need to sort them
				sort.Ints(lookaheads)

				// remove all the epsilons from the first set
				n := 0
				for _, l := range lookaheads {
					// -1 == epsilon
					if l != -1 {
						lookaheads[n] = l
						n++
					}
				}

				// if any epsilons were removed, the desired length of the
				// new slice will change so we can use it to test if there
				// were an epsilons in the first set
				if n != len(lookaheads) {
					lookaheads = lookaheads[:n]
					lookaheads = combineLookaheads(lookaheads, item.Lookaheads)
				}
			}

			newRuleRefs := ptb.BNFRules.ProdRules[string(nt)]

			// allocate a base buffer new items (based on number of new rules)
			newItems := make([]*LRItem, len(newRuleRefs))

			for i := range newItems {
				newItems[i] = &LRItem{Rule: newRuleRefs[i], DotPos: 0, Lookaheads: lookaheads}
			}

			newItemSet := &LRItemSet{Items: newItems}

			// ensure that new item kernel is ordered
			newItemSet.order()

			// recursively calculate closure
			newItemSet = ptb.closureOf(newItemSet)

			kernel = union(kernel, newItemSet)
		}
	}

	// the new kernel is already implicitly ordered (by union)
	return kernel
}

// first calculates the first set of a given slice of a BNF rule (can be used
// recursively) NOTE: should not be called with an empty slice (will fail)
func (ptb *PTableBuilder) first(ruleSlice []interface{}) []int {
	if nt, ok := ruleSlice[0].(BNFNonterminal); ok {
		var firstSet []int

		// start by accumulating all firsts of nonterminal (inc. epsilon) to
		// start with (in next block).  NOTE: string(nt) should be "free"

		// check to see if the nonterminal first set has already been calculated
		if mfs, ok := ptb.firstSets[string(nt)]; ok {
			firstSet = make([]int, len(mfs))

			// copy so that our in-place mutations of the first set don't affect
			// the memoized version (small cost but ultimately trivial)
			copy(mfs, firstSet)
		} else {
			for _, rRef := range ptb.BNFRules.ProdRules[string(nt)] {
				// r will never be empty
				ntFirst := ptb.first(ptb.BNFRules.Rules[rRef].Contents)

				firstSet = append(firstSet, ntFirst...)
			}

			// memoize the base first set (copied for same reasons as above)
			mfs := make([]int, len(firstSet))
			copy(firstSet, mfs)
			ptb.firstSets[string(nt)] = mfs
		}

		// if there are no elements following a given production, then any
		// epsilons will remain in the first set (as nothing follows)
		if len(ruleSlice) == 1 {
			return firstSet
		}

		// if there are elements that follow the current element in our rule,
		// then we need to check for and remove epsilons in the first set (as
		// they may not be necessary)
		n := 0
		for _, f := range firstSet {
			if f != -1 {
				firstSet[n] = f
				n++
			}
		}

		// if the length has changed, epsilon values were removed and therefore,
		// we need to consider the firsts of what follows our first element as
		// valid firsts for the rule (ie. Fi(Aw) = Fi(A) \ { epsilon} U Fi(w))
		if n != len(firstSet) {
			// now we can trim off the excess length
			firstSet := firstSet[:n]

			// can blindly call first here because we already checked for rule
			// slices that could result in a runtime panic (ie. will be empty)
			firstSet = append(firstSet, ptb.first(ruleSlice[1:])...)
		}
	} else if _, ok := ruleSlice[0].(BNFEpsilon); ok {
		// convert epsilon into an integer value (-1) and apply Fi(epsilon) =
		// {epsilon }.  NOTE: epsilons only occur as solitary rules (always
		// valid)
		return []int{-1}
	}

	// apply Fi(w) = { w } where w is a terminal
	return []int{int(ruleSlice[0].(BNFTerminal))}
}

// compare is used to compare two items so that they can be ordered: -1 => a <
// b; 1 => a > b; 0 => a == b.  ASSUMES LOOKAHEADS ARE ALREADY SORTED.
func compare(a, b *LRItem) int {
	if a.Rule < b.Rule {
		return -1
	} else if a.Rule > b.Rule {
		return 1
	}

	if a.DotPos < b.DotPos {
		return -1
	} else if a.DotPos > b.DotPos {
		return 1
	}

	if len(a.Lookaheads) < len(b.Lookaheads) {
		return -1
	} else if len(a.Lookaheads) > len(b.Lookaheads) {
		return 1
	}

	for i, l := range a.Lookaheads {
		if l < b.Lookaheads[i] {
			return -1
		} else if l > b.Lookaheads[i] {
			return 1
		}
	}

	return 0
}

// combineLookaheads takes two sets of lookaheads and produces their ordered
// union (as a copy).  Base sets of lookaheads must be ORDERED!  NOTE: this
// function uses the same base algorithm as the union of two item sets
func combineLookaheads(a, b []int) []int {
	newLookaheads := make([]int, len(a)+len(b))

	insertPosition, i, j := 0, 0, 0
	for ; i < len(a) && j < len(b); insertPosition++ {
		if a[i] < b[j] {
			newLookaheads[insertPosition] = a[i]
			i++
		} else if a[i] > b[j] {
			newLookaheads[insertPosition] = b[j]
			j++
		} else {
			newLookaheads[insertPosition] = a[i]
			i++
			j++
		}
	}

	for ; i < len(a); i++ {
		newLookaheads[insertPosition] = a[i]
		insertPosition++
	}

	for ; j < len(b); j++ {
		newLookaheads[insertPosition] = b[j]
		insertPosition++
	}

	newLookaheads = newLookaheads[:insertPosition]
	return newLookaheads
}

// addToSet adds an item provided it is not already in the set
func (itemSet *LRItemSet) addToSet(newItem *LRItem) {
	for _, item := range itemSet.Items {
		if reflect.DeepEqual(item, newItem) {
			return
		}
	}

	itemSet.Items = append(itemSet.Items, newItem)
}

// union combines two item sets together without duplicating
// their elements.  It returns a new item set.  The sets
// MUST BE ORDERED before their union can be determined.
// Moreover, the union of the two sets will remain ordered.
func union(a, b *LRItemSet) *LRItemSet {
	aItems, bItems := a.Items, b.Items

	// assume union will remove no items
	newItems := make([]*LRItem, len(aItems)+len(bItems))

	// perform the initial merge
	insertPosition, i, j := 0, 0, 0
	for ; i < len(aItems) && j < len(bItems); insertPosition++ {
		switch compare(aItems[i], bItems[j]) {
		case 0:
			// equal => increment both
			newItems[insertPosition] = aItems[i]
			i++
			j++
		case 1:
			// a > b => take from b and inc. j
			newItems[insertPosition] = bItems[j]
			j++
		case -1:
			// a < b => take from a and inc i
			newItems[insertPosition] = aItems[i]
			i++
		}
	}

	// add in all lingering items - one set already emptied so no need to check
	// for order here (like merge sort :D)
	for ; i < len(aItems); i++ {
		newItems[insertPosition] = aItems[i]
		insertPosition++
	}

	for ; j < len(bItems); j++ {
		newItems[insertPosition] = bItems[j]
		insertPosition++
	}

	// slice down to actual size (insert position :D)
	newItems = newItems[:insertPosition]

	// assumes no connections have been made
	return &LRItemSet{Items: newItems}
}

// order sorts rules of item set (SHOULD BE CALLED ONCE).  It is used for
// efficient comparison when generating parsing table (sorting once is less
// expensive than comparing a list of unordered rules for equality)
func (itemSet *LRItemSet) order() {
	sort.Slice(itemSet.Items, func(i, j int) bool {
		return compare(itemSet.Items[i], itemSet.Items[j]) == -1
	})
}

// equals tests if two item sets are equal AFTER THEY HAVE BEEN SORTED
func (itemSet *LRItemSet) equals(other *LRItemSet) bool {
	for i, item := range itemSet.Items {
		if compare(item, other.Items[i]) != 0 {
			return false
		}
	}

	return true
}

// addConnection adds the given element as a connection to the given item set
func (itemSet *LRItemSet) addConnection(element interface{}, setPos int) {
	switch v := element.(type) {
	case BNFTerminal:
		itemSet.TerminalConns[int(v)] = setPos
	case BNFNonterminal:
		itemSet.NonterminalConns[string(v)] = setPos
	}
}
