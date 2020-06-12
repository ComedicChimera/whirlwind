package syntax

import (
	"fmt"
	"os"
	"reflect"
	"sort"
)

const _goalSymbol = "file"

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
	BNFRules *BNFRuleTable
	Table    *ParsingTable

	ItemSets []*LRItemSet

	// allows us to memoize first sets by nonterminals
	firstSets map[string][]int
}

// constructParsingTable takes in an input rule table and attempt to build a
// parsing table for it.  If it fails, a descriptive error is returned.  If it
// succeeds a full parsing table is returned.  NOTE: this function resolves
// shift-reduce conflicts in favor of SHIFT (will warn if it does this)!
func constructParsingTable(brt *BNFRuleTable) (*ParsingTable, bool) {
	ptableBuilder := PTableBuilder{BNFRules: brt, firstSets: make(map[string][]int)}

	if !ptableBuilder.Build() {
		return nil, false
	}

	return ptableBuilder.Table, true
}

// Build uses the current builds a full parsing table for a given rule table
func (ptb *PTableBuilder) Build() bool {
	// augment the rule set
	augRuleNdx := len(ptb.BNFRules.RulesByIndex)
	ptb.BNFRules.RulesByIndex = append(ptb.BNFRules.RulesByIndex,
		&BNFRule{ProdName: "_END_", Contents: []BNFElement{BNFNonterminal(_goalSymbol)}})
	ptb.BNFRules.RulesByProdName["_END_"] = []int{augRuleNdx}

	// create the starting kernel
	startKernel := &LRItemSet{Items: []*LRItem{&LRItem{Rule: augRuleNdx, DotPos: 0, Lookaheads: []int{EOF}}}}

	// calculate its closure to get the starting set
	startSet := ptb.closureOf(startKernel)

	// add that set to the list item sets (so it can be recognized in nextSets)
	ptb.ItemSets = append(ptb.ItemSets, startSet)

	// calculate the next sets from the starting item set
	ptb.nextSets(startSet)

	// the number of states (rows) will be equivalent to the number of item sets
	// since the merging has already occurred in generating the item sets
	ptb.Table = &ParsingTable{Rows: make([]*PTableRow, len(ptb.ItemSets))}

	// used to keep track of which PTableRules have already been generated so
	// that any rules that will become redundant can be mapped to already
	// existing rules (ie. rules that have the same name and size)
	ptableRules := make(map[string][]int)

	tableConstructionSucceeded := true

	for i, itemSet := range ptb.ItemSets {
		row := &PTableRow{Actions: make(map[int]*Action), Gotos: make(map[string]int)}
		ptb.Table.Rows[i] = row

		// calculate the necessary shift actions
		for terminal, state := range itemSet.TerminalConns {
			row.Actions[terminal] = &Action{Kind: AKShift, Operand: state}
		}

		// populate our GOTO table
		for nt, state := range itemSet.NonterminalConns {
			row.Gotos[nt] = state
		}

		// place any reduce actions in the table as necessary
		for _, item := range itemSet.Items {
			bnfRule := ptb.BNFRules.RulesByIndex[item.Rule]

			// dot is at the end of a rule/epsilon rule => reduction time
			if item.DotPos == len(bnfRule.Contents) || reflect.DeepEqual(bnfRule.Contents[0], BNFEpsilon{}) {
				// determine the correct reduction rule
				reduceRule := -1

				// convert the BNF rule into a ptable rule
				convertedRule := &PTableRule{Name: bnfRule.ProdName}
				if bnfRule.Contents[0].Kind() == BNFKindEpsilon {
					convertedRule.Count = 0
				} else {
					convertedRule.Count = len(bnfRule.Contents)
				}

				if addedRules, ok := ptableRules[convertedRule.Name]; ok {
					for _, ruleRef := range addedRules {
						if ptb.Table.Rules[ruleRef].Count == convertedRule.Count {
							reduceRule = ruleRef
							break
						}
					}

					// if rule is still -1, it has not been added => not redundant
					if reduceRule == -1 {
						reduceRule = len(ptb.Table.Rules)
						ptb.Table.Rules = append(ptb.Table.Rules, convertedRule)

						// add our rule to the ptableRules entry for the production name
						ptableRules[convertedRule.Name] = append(ptableRules[convertedRule.Name], reduceRule)
					}
				} else {
					// if it hasn't even been added to ptableRules, we need to
					// create it and create an entry for it in ptableRules.
					reduceRule = len(ptb.Table.Rules)
					ptb.Table.Rules = append(ptb.Table.Rules, convertedRule)

					ptableRules[convertedRule.Name] = []int{reduceRule}
				}

				// attempt to add all of the corresponding reduce actions
				for _, lookahead := range item.Lookaheads {
					if action, ok := row.Actions[lookahead]; ok {
						// action already exists.  If it is a shift action,
						// we warn but do not error.  If it is another reduce
						// action, we print out the error and return that
						// the table construction was unsuccessful.
						if action.Kind == AKShift {
							// find a better way to indicate the token value
							fmt.Printf("Shift/Reduce Conflict Resolved Between `%s` and `%d`\n", bnfRule.ProdName, lookahead)
						} else if action.Kind == AKReduce {
							oldRule, newRule := ptb.Table.Rules[action.Operand], ptb.Table.Rules[reduceRule]

							// if there is a conflict between two epsilon rules,
							// it is not actually a conflict (empty trees are
							// pruned - name doesn't actually matter here)
							if oldRule.Count == 0 && newRule.Count == 0 {
								continue
							} else if *oldRule == *newRule {
								// if the rules create the same tree and consume
								// the same amount of tokens, we don't care if
								// they contain different elements => they are
								// effectively equal (so no real conflict exists)
								continue
							}

							fmt.Printf("Reduce/Reduce Conflict Between `%s` and `%s`\n", oldRule.Name, newRule.Name)
							tableConstructionSucceeded = false
						}

						// ACCEPT collisions should never happen
					} else if lookahead == EOF && bnfRule.ProdName == "_END_" {
						// if we are trying to reduce the GOAL symbol with an
						// EOF lookahead we want to mark this action as accept
						row.Actions[lookahead] = &Action{Kind: AKAccept}
					} else {
						row.Actions[lookahead] = &Action{Kind: AKReduce, Operand: reduceRule}
					}
				}
			}
		}
	}

	return tableConstructionSucceeded
}

// TODO: remove when no longer needed
func (ptb *PTableBuilder) printSets() {
	for _, itemSet := range ptb.ItemSets {
		fmt.Print("{")

		for _, item := range itemSet.Items {
			rule := ptb.BNFRules.RulesByIndex[item.Rule]
			fmt.Print(rule.ProdName, "-> ")

			for i, elem := range rule.Contents {
				if i == item.DotPos {
					fmt.Print(".")
				}

				switch r := elem.(type) {
				case BNFEpsilon:
					fmt.Print("''")
				case BNFNonterminal, BNFTerminal:
					fmt.Print(r)
				}

				if i < len(rule.Contents)-1 {
					fmt.Print(" ")
				}
			}

			if item.DotPos == len(rule.Contents) {
				fmt.Print(".")
			}

			fmt.Print(", ")

			for i, lookahead := range item.Lookaheads {
				fmt.Print(lookahead)

				if i < len(item.Lookaheads)-1 {
					fmt.Print("/")
				}

			}

			fmt.Print("; ")
		}

		fmt.Print("}\n")
	}
}

// addRule converts a BNF rule into a usable rule (assumes rule is not redundant)
// and adds it to the rule set.  Returns the a reference to the new rule.
func (ptb *PTableBuilder) addRule(bnfRule *BNFRule) int {
	// We know the next rule will be at the end of rules so we can get the rule
	// reference preemtively from the length
	reduceRule := len(ptb.Table.Rules)

	var count int
	if _, ok := bnfRule.Contents[0].(BNFEpsilon); ok {
		// epsilon rules consume no tokens to make their tree
		count = 0
	} else {
		count = len(bnfRule.Contents)
	}

	ptb.Table.Rules = append(ptb.Table.Rules,
		&PTableRule{Name: bnfRule.ProdName, Count: count})

	return reduceRule
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
		ruleContents := ptb.BNFRules.RulesByIndex[item.Rule].Contents

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
		// not to add it to graph (and thereby what to connect it to)
		gotoSet := ptb.gotoOf(startSet, dottedElem)

		gotoMatched := false
		for i, set := range ptb.ItemSets {
			// NOTE: merge will attempt to merge to sets that have the same
			// core. If the have identical lookaheads, then this is equivalent
			// to checking equality.  If they don't, it facilitates step-by-step
			// merging for sets that have the same cores.
			if set.merge(gotoSet) {
				// if we have already calculated the state, create a connection
				// back to that precreated state, set the matched flag, and exit
				// the loop (b/c we don't need to search anymore)
				gotoMatched = true
				startSet.addConnection(dottedElem, i)

				// next, we need to propagate the lookaheads of the merged set
				// to its connections (as GOTO would => gives correct behavior)
				ptb.propagateLookaheads(set, []int{i})

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
// given input (NOT EPSILON).  NOTE: will ignore items with dots at the end.
func (ptb *PTableBuilder) gotoOf(itemSet *LRItemSet, element BNFElement) *LRItemSet {
	newItemSet := &LRItemSet{}

	for _, item := range itemSet.Items {
		ruleContents := ptb.BNFRules.RulesByIndex[item.Rule].Contents

		if item.DotPos == len(ruleContents) {
			continue
		}

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
	for addedItems := true; addedItems; {
		addedItems = false

		for _, item := range kernel.Items {
			ruleContents := ptb.BNFRules.RulesByIndex[item.Rule].Contents

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

				newRuleRefs, ok := ptb.BNFRules.RulesByProdName[string(nt)]

				if !ok {
					fmt.Printf("Grammar Error: No production defined for nonterminal `%s`", string(nt))
					os.Exit(1)
				}

				// allocate a base buffer new items (based on number of new rules)
				newItems := make([]*LRItem, len(newRuleRefs))

				// productions will never had duplicate rule references so no
				// merging is required here (ie. order will work as desired)
				for i := range newItems {
					newItems[i] = &LRItem{Rule: newRuleRefs[i], DotPos: 0, Lookaheads: lookaheads}
				}

				newItemSet := &LRItemSet{Items: newItems}

				// ensure that new item kernel is ordered
				newItemSet.order()

				// combine our original kernel with our new kernel
				// to see if there are any new items being added
				initKSize := len(kernel.Items)
				kernel = union(kernel, newItemSet)

				// if our kernel did not change size, no new items
				// were added and there is no need to calculate closure
				if len(kernel.Items) == initKSize {
					continue
				}

				// items have been added and therefore we should continue
				// calculating closure (and set the flag accordingly)
				addedItems = true
			}
		}
	}

	// the new kernel is already implicitly ordered (by union)
	return kernel
}

// first calculates the first set of a given slice of a BNF rule (can be used
// recursively) NOTE: should not be called with an empty slice (will fail)
func (ptb *PTableBuilder) first(ruleSlice []BNFElement) []int {
	if nt, ok := ruleSlice[0].(BNFNonterminal); ok {
		var firstSet []int

		// start by accumulating all firsts of nonterminal (inc. epsilon) to
		// start with (in next block).  NOTE: string(nt) should be "free"

		// check to see if the nonterminal first set has already been calculated
		if mfs, ok := ptb.firstSets[string(nt)]; ok {
			firstSet = make([]int, len(mfs))

			// copy so that our in-place mutations of the first set don't affect
			// the memoized version (small cost but ultimately trivial)
			copy(firstSet, mfs)
		} else {
			for _, rRef := range ptb.BNFRules.RulesByProdName[string(nt)] {
				// r will never be empty
				ntFirst := ptb.first(ptb.BNFRules.RulesByIndex[rRef].Contents)

				firstSet = append(firstSet, ntFirst...)
			}

			// memoize the base first set (copied for same reasons as above)
			mfs := make([]int, len(firstSet))
			copy(mfs, firstSet)
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

		return firstSet
	} else if _, ok := ruleSlice[0].(BNFEpsilon); ok {
		// convert epsilon into an integer value (-1) and apply Fi(epsilon) =
		// {epsilon }.  NOTE: epsilons only occur as solitary rules (always
		// valid)
		return []int{-1}
	}

	// apply Fi(w) = { w } where w is a terminal
	return []int{int(ruleSlice[0].(BNFTerminal))}
}

// propagateLookaheads recursively updates the lookaheads of all connections of
// a set after a merge to ensure lookaheads propagate properly (ie. like GOTO)
func (ptb *PTableBuilder) propagateLookaheads(itemSet *LRItemSet, prevConns []int) {
	doPropagate := func(elem BNFElement, conn int) {
	loop:
		for _, item := range itemSet.Items {
			ruleContents := ptb.BNFRules.RulesByIndex[item.Rule].Contents

			// if our dot is at the end of a rule, nothing to propagate
			if item.DotPos < len(ruleContents) {
				dottedElem := ruleContents[item.DotPos]

				for _, prevConn := range prevConns {
					if prevConn == conn {
						continue loop
					}
				}

				if reflect.DeepEqual(dottedElem, elem) {
					connSet := ptb.ItemSets[conn]

					for _, connItem := range connSet.Items {
						connItem.Lookaheads = combineLookaheads(item.Lookaheads, connItem.Lookaheads)
					}

					ptb.propagateLookaheads(connSet, append(prevConns, conn))
				}
			}
		}
	}

	for tVal, tConn := range itemSet.TerminalConns {
		doPropagate(BNFTerminal(tVal), tConn)
	}

	for nVal, nConn := range itemSet.NonterminalConns {
		doPropagate(BNFNonterminal(nVal), nConn)
	}
}

// compare is used to compare two items so that they can be ordered: -1 => a <
// b; 1 => a > b; 0 => a == b.   Lookaheads are not included: two items with same
// core and different lookaheads should be merged (same lookaheads)
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
			// equal => increment both, combine lookaheads
			mergedItem := aItems[i]
			mergedItem.Lookaheads =
				combineLookaheads(mergedItem.Lookaheads, bItems[j].Lookaheads)

			newItems[insertPosition] = mergedItem
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
// expensive than comparing a list of unordered rules for equality).  It ASSUMES
// all items with the same core have been merged as necessary.
func (itemSet *LRItemSet) order() {
	sort.Slice(itemSet.Items, func(i, j int) bool {
		return compare(itemSet.Items[i], itemSet.Items[j]) == -1
	})
}

// merge attempts to merge to item sets.  It returns true if such a merge was
// possible and performs the merge (in-place).  If not, it returns false.
func (itemSet *LRItemSet) merge(other *LRItemSet) bool {
	// if they have different lengths, they have different cores
	if len(itemSet.Items) != len(other.Items) {
		return false
	}

	// test if the items have the same core
	for i, item := range itemSet.Items {
		otherItem := other.Items[i]

		if item.Rule != otherItem.Rule || item.DotPos != otherItem.DotPos {
			return false
		}
	}

	// if they do, perform the merge by combining the lookaheads
	for i, item := range itemSet.Items {
		itemSet.Items[i].Lookaheads = combineLookaheads(item.Lookaheads, other.Items[i].Lookaheads)
	}

	return true
}

// addConnection adds the given element as a connection to the given item set
func (itemSet *LRItemSet) addConnection(element BNFElement, setPos int) {
	switch v := element.(type) {
	case BNFTerminal:
		itemSet.TerminalConns[int(v)] = setPos
	case BNFNonterminal:
		itemSet.NonterminalConns[string(v)] = setPos
	}
}
