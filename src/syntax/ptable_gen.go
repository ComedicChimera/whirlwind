package syntax

import (
	"fmt"
	"os"
	"reflect"
)

const _goalSymbol = "file"

// LRItem represents an LR(0) item
type LRItem struct {
	// Rule refers to the rule number in the RuleTable not
	// the final rule number in the parsing table
	Rule int

	// DotPos refers to the index the dot is considered to
	// be placed BEFORE (so a dot at the end of the item
	// would have a dot pos == to the length of the rule)
	DotPos int
}

// LRItemSet represents a complete LR(1) state
type LRItemSet struct {
	// Items matches each LRItem with its lookaheads thereby rendering each
	// entry an LR(1) item.  The lookaheads are stored as map for fast merging
	Items map[LRItem]map[int]struct{}

	// Conns represents all of the possible progressions for a given item set
	Conns map[BNFElement]int
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
	startSet := &LRItemSet{Items: map[LRItem]map[int]struct{}{
		LRItem{Rule: augRuleNdx, DotPos: 0}: map[int]struct{}{EOF: struct{}{}},
	}}

	// calculate its closure to get the starting set
	ptb.closureOf(startSet)

	// add that set to the list item sets (so it can be recognized in nextSets)
	ptb.ItemSets = append(ptb.ItemSets, startSet)

	// calculate the next sets from the starting item set
	ptb.nextSets(startSet)

	// now, we need to propagate the lookaheads of merged item sets
	ptb.propagateLookaheads(startSet, map[int]struct{}{0: struct{}{}})

	// the number of states (rows) will be equivalent to the number of item sets
	// since the merging has already occurred in generating the item sets
	ptb.Table = &ParsingTable{Rows: make([]*PTableRow, len(ptb.ItemSets))}

	return ptb.buildTableFromSets()
}

// buildTableFromSets attempts to convert the itemset graph into a parsing table
// and returns a boolean indicating whether or not that operation was successful
func (ptb *PTableBuilder) buildTableFromSets() bool {
	// used to keep track of which PTableRules have already been generated so
	// that any rules that will become redundant can be mapped to already
	// existing rules (ie. rules that have the same name and size)
	ptableRules := make(map[string][]int)

	tableConstructionSucceeded := true

	for i, itemSet := range ptb.ItemSets {
		row := &PTableRow{Actions: make(map[int]*Action), Gotos: make(map[string]int)}
		ptb.Table.Rows[i] = row

		// calculate the necessary actions based on our connections
		for conn, state := range itemSet.Conns {
			switch v := conn.(type) {
			case BNFTerminal:
				row.Actions[int(v)] = &Action{Kind: AKShift, Operand: state}
			case BNFNonterminal:
				row.Gotos[string(v)] = state
			}
		}

		// place any reduce actions in the table as necessary
		for item, lookaheads := range itemSet.Items {
			bnfRule := ptb.BNFRules.RulesByIndex[item.Rule]

			// dot is at the end of a rule/epsilon rule => reduction time
			if item.DotPos == len(bnfRule.Contents) || reflect.DeepEqual(bnfRule.Contents[0], BNFEpsilon{}) {
				// determine the correct reduction rule
				reduceRule := -1

				// if we are reducing by the _END_ rule, we are accepting
				// meaning, we don't actually need to store or calculate a rule,
				// but if we are reducing by anything else then we do
				if bnfRule.ProdName != "_END_" {
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
				}

				// attempt to add all of the corresponding reduce actions
				for lookahead := range lookaheads {
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
					} else if reduceRule == -1 {
						// if our reduce rule is -1, we are reducing the goal
						// symbol which means we should accept (no test for EOF)
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
	// create a map to store the goto sets as we build them up
	gotoSets := make(map[BNFElement]*LRItemSet)

	// first compute the kernels of all of the possible goto sets
	for item, lookaheads := range startSet.Items {
		ruleContents := ptb.BNFRules.RulesByIndex[item.Rule].Contents

		// if the dot is at the end of the rule or there is an epsilon in the
		// rule (which implies due to the construction of our rule table that it
		// is an epsilon rule), then there are no further sets to create
		if item.DotPos == len(ruleContents) || ruleContents[item.DotPos].Kind() == BNFKindEpsilon {
			continue
		}

		// create a new item from the current item with the dot moved forward one
		newItem := LRItem{Rule: item.Rule, DotPos: item.DotPos + 1}

		// determine whether or not to create a new connected set for our item
		// or to add to a preexisting one by checking if a connection exists
		dottedElem := ruleContents[item.DotPos]

		// we can add items directly here since they all have the same lookaheads
		if gotoSet, ok := gotoSets[dottedElem]; ok {
			gotoSet.Items[newItem] = lookaheads
		} else {
			gotoSet := &LRItemSet{Items: map[LRItem]map[int]struct{}{newItem: lookaheads}}
			gotoSets[dottedElem] = gotoSet
		}
	}

	// assume that the starting set's connections map is not initialized
	startSet.Conns = make(map[BNFElement]int)

	// now that we have computed our kernel goto sets, we need to calculate
	// their closures and determine whether or not to add them to the set graph
	// or merge them with another set.  In this pass, we can also add the
	// appropriate connections and mark (by removal) which goto sets should be
	// recursively expanded from (via. calls to nextSets).
	for elem, gotoSet := range gotoSets {
		ptb.closureOf(gotoSet)

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
				startSet.Conns[elem] = i

				// remove the created goto set since we are no longer using it
				delete(gotoSets, elem)

				break
			}
		}

		if !gotoMatched {
			// add the connection to our goto set (predictively)
			startSet.Conns[elem] = len(ptb.ItemSets)

			// add the goto set to our graph
			ptb.ItemSets = append(ptb.ItemSets, gotoSet)
		}
	}

	// progress through all remaining new goto sets and recursively calculate
	// their next sets
	for _, gotoSet := range gotoSets {
		ptb.nextSets(gotoSet)
	}
}

// closureOf calculates the closure of the kernel of an item set in-place.
func (ptb *PTableBuilder) closureOf(kernel *LRItemSet) {
	// calculated closures is used to store the LR items for which a closure has
	// already been calculated (prevents repeat calculation - does not store results)
	calculatedClosures := make(map[LRItem]struct{})

	for addedItems := true; addedItems; {
		addedItems = false

		for item, itemLookaheads := range kernel.Items {
			ruleContents := ptb.BNFRules.RulesByIndex[item.Rule].Contents

			if _, ok := calculatedClosures[item]; ok || item.DotPos == len(ruleContents) {
				continue
			}

			if nt, ok := ruleContents[item.DotPos].(BNFNonterminal); ok {
				var lookaheads map[int]struct{}

				if item.DotPos == len(ruleContents)-1 {
					lookaheads = itemLookaheads
				} else {
					firstSet := ptb.first(ruleContents[item.DotPos+1:])

					// remove all the epsilons from the first set
					n := 0
					for _, l := range firstSet {
						// -1 == epsilon
						if l != -1 {
							firstSet[n] = l
							n++
						}
					}

					// if any epsilons were removed, the desired length of the
					// new slice will change so we can use it to test if there
					// were an epsilons in the first set
					epsilonsRemoved := n != len(firstSet)
					firstSet = firstSet[:n]

					lookaheads = make(map[int]struct{})
					for _, first := range firstSet {
						lookaheads[first] = struct{}{}
					}

					if epsilonsRemoved {
						lookaheads = combineLookaheads(lookaheads, itemLookaheads)
					}
				}

				newRuleRefs, ok := ptb.BNFRules.RulesByProdName[string(nt)]

				if !ok {
					fmt.Printf("Grammar Error: No production defined for nonterminal `%s`", string(nt))
					os.Exit(1)
				}

				// allocate a map to hold the new items
				newItems := make(map[LRItem]map[int]struct{})

				for i := range newRuleRefs {
					newItems[LRItem{Rule: newRuleRefs[i], DotPos: 0}] = lookaheads
				}

				newItemSet := &LRItemSet{Items: newItems}

				// combine our original kernel with our new kernel
				// to see if there are any new items being added
				initKSize := len(kernel.Items)
				for newItem := range newItemSet.Items {
					if kernelLookaheads, ok := kernel.Items[newItem]; ok {
						kernel.Items[newItem] = combineLookaheads(kernelLookaheads, lookaheads)
					} else {
						kernel.Items[newItem] = lookaheads
					}
				}

				// mark that our items closure has been calculated
				calculatedClosures[item] = struct{}{}

				// items have been added and therefore we should continue
				// calculating closure (and set the flag accordingly)
				if len(kernel.Items) != initKSize {
					addedItems = true
				}
			}
		}
	}
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
			firstSet = firstSet[:n]

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
func (ptb *PTableBuilder) propagateLookaheads(itemSet *LRItemSet, prevConns map[int]struct{}) {
	for elem, conn := range itemSet.Conns {
		if _, ok := prevConns[conn]; !ok {
			continue
		}

		for item, lookaheads := range itemSet.Items {
			ruleContents := ptb.BNFRules.RulesByIndex[item.Rule].Contents

			// if our dot is at the end of a rule, nothing to propagate
			if item.DotPos < len(ruleContents) {
				dottedElem := ruleContents[item.DotPos]

				if reflect.DeepEqual(dottedElem, elem) {
					connSet := ptb.ItemSets[conn]

					for connItem, connLookaheads := range connSet.Items {
						connSet.Items[connItem] = combineLookaheads(lookaheads, connLookaheads)
					}

					prevConns[conn] = struct{}{}
					ptb.propagateLookaheads(connSet, prevConns)
				}
			}
		}
	}

}

// combineLookaheads takes two sets of lookaheads and produces their union
func combineLookaheads(a, b map[int]struct{}) map[int]struct{} {
	newLookaheads := make(map[int]struct{})

	for lookahead := range a {
		newLookaheads[lookahead] = struct{}{}
	}

	for lookahead := range b {
		newLookaheads[lookahead] = struct{}{}
	}

	return newLookaheads
}

// merge attempts to merge to item sets.  It returns true if such a merge was
// possible and performs the merge (in-place).  If not, it returns false.
func (itemSet *LRItemSet) merge(other *LRItemSet) bool {
	// if they have different lengths, they have different cores
	if len(itemSet.Items) != len(other.Items) {
		return false
	}

	// test if the items have the same core
	for item := range itemSet.Items {
		if _, ok := other.Items[item]; !ok {
			return false
		}
	}

	// if they do, perform the merge by combining the lookaheads
	for otherItem, otherLookaheads := range other.Items {
		itemSet.Items[otherItem] = combineLookaheads(itemSet.Items[otherItem], otherLookaheads)
	}

	return true
}
