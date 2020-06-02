package syntax

// LRItem represents an item in a set of items used to generate the action-goto
// table for the parser.  It stores the production name, rule number, and dot position
// as well as a look ahead (that is the Name of a token).  It is an LR(1) item.
type LRItem struct {
	Name         string
	Rule, DotPos int
	Lookahead    string
}

// TableGenerator is a construct that stores the shared state of the LALR(1)
// parsing table generator.
type TableGenerator struct {
	Items    []*LRItem
	SGrammar SimplifiedGrammar
}

// closure represents the standard closure for function for the construction of
// an LALR(1) action-goto table for a given set of items
func (tg *TableGenerator) closure(items []*LRItem) []*LRItem {
	// first assume that there are items to add to the preexisting set
	for itemsAdded := true; itemsAdded; {
		// then proceed to check if items were indeed added (if they were, this
		// flag gets set back to true before the next iteration)
		itemsAdded = false

		// go through each item and get its corresponding rules
		for _, item := range items {
			for i, rule := range tg.SGrammar[item.Name] {
				// calculate the firsts of the rule and added them to the set
				// (this behavior is outlined on p. 261 of the Dragon Book)
				for _, terminal := range tg.first(rule) {
					var added bool
					items, added = addNewToSet(items, item.Name, i, terminal)

					itemsAdded = itemsAdded || added
				}
			}
		}
	}

	return items
}

// finds the firsts of a given *rule*. it is intended to be used recursively
// meaning it will accept slices of rules (ie. it simply finds the firsts of the
// set of elements it is given)
func (tg *TableGenerator) first(rule []*SGElement) []string {
	if rule[0].Kind == SGENonterminal {
		var firstSet []string

		// accumulate all of the firsts of the nonterminal before applying
		// additional first calculation logic
		for _, r := range tg.SGrammar[rule[0].Value] {
			ntFirst := tg.first(r)

			firstSet = append(firstSet, ntFirst...)
		}

		// if there are no elements following a given production, then any
		// epsilons will remain in the first set (as nothing follows)
		if len(rule) == 1 {
			return firstSet
		}

		// if there are more elements that follow the current element in our
		// rule, then we first remove any epsilons from our first set
		n := 0
		for _, f := range firstSet {
			if f != "" {
				firstSet[n] = f
				n++
			}
		}

		// if the length has changed, epsilon values were removed and therefore,
		// we need to consider the firsts of what follows our first element as
		// valid firsts for the rule (ie. Fi(Aw') = Fi(A) \ { epsilon } U
		// Fi(w'))
		if n != len(firstSet) {
			firstSet = firstSet[:n]
			firstSet = append(firstSet, tg.first(rule[1:])...)
		}

		return firstSet
	}

	// catches both terminals and epsilon since Fi(epsilon) = { epsilon } and
	// Fi(a) where a is a terminal = { a }
	return []string{rule[0].Value}
}

// UTILITY FUNCTIONS
// -----------------

// addToSet adds a precreated LRItem to a set of LRItems and returns a new set
// along with a boolean flag indicated whether or not an item was actually added
func addToSet(set []*LRItem, item *LRItem) ([]*LRItem, bool) {
	for _, sItem := range set {
		if *sItem == *item {
			return nil, false
		}
	}

	return append(set, item), true
}

// addNewToSet has the same behavior as addToSet with the change being it assumes
// that a new LRItem is being created (in the context of its use in `closure`)
func addNewToSet(set []*LRItem, name string, rule int, terminal string) ([]*LRItem, bool) {
	return addToSet(set, &LRItem{Name: name, Rule: rule, DotPos: 0, Lookahead: terminal})
}
