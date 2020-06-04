package syntax

import (
	"reflect"
	"sort"
)

// NOTE TO READER: This implementation is based off of a variety of sources and
// is likely not the most efficient to construct an LALR(1) parsing table.  I
// chose to implement the algorithm myself both as a learning experience and to
// give me tight control over the parser's behavior.  Also, I wasn't sure about
// tool compatability with Go.  Special Thanks to:
// - Ravindrababu Ravula (Youtube) - I literally would have beaten myself to
// death with the Dragon Book were it not for this absolute CHAD of a human
// being.
// - Stephen Jackson (random dude on the interwebs) - Although my implementation
// isn't based on your description, you convinced me (indirectly) that this
// thing was actually implementable without years of deep study.
// ALSO: This doesn't actually run most of the time (used during development as
// a parser generator so ¯\_(ツ)_/¯).

// LRItem represents an LR(1) item used to construct the LALR(1) parsing table.
type LRItem struct {
	Name       string
	Rule       []*SGElement
	DotPos     int
	Lookaheads []string
}

// addItem takes a slice of LRItems and a new LRItem and attempts to add the
// LRItem to the set.  It returns the newly formed set (as a copy)
func addItem(itemSet []*LRItem, item *LRItem) []*LRItem {
	for _, setItem := range itemSet {
		if reflect.DeepEqual(setItem, item) {
			return itemSet
		}
	}

	return append(itemSet, item)
}

// union takes two slices of LRItems and returns a slice representing their union
func union(a, b []*LRItem) []*LRItem {
	for _, item := range b {
		a = addItem(a, item)
	}

	return a
}

// order takes a slice of LRItems and puts them in a consistent order so that if
// two states are compared via equals that contain equivalent items, they will
// be identified as equal (small optimization to not sort on every comparison).
// NOTE: this sort is in-place (reduced copying on sorting)!  Since it should
// only be called once per state, the performance impact should be manageable.
func order(items []*LRItem) {
	sort.Slice(items, func(i, j int) bool {
		iitem, jitem := items[i], items[j]

		if len(iitem.Rule) < len(jitem.Rule) {
			return true
		}

		// fix ordering algo

		if len(iitem.Lookaheads) < len(jitem.Lookaheads) {
			return true
		} else if len(iitem.Lookaheads) > len(jitem.Lookaheads) {
			return false
		}

		sort.Strings(iitem.Lookaheads)
		sort.Strings(jitem.Lookaheads)

		for k, i1 := range iitem.Lookaheads {
			if i1 < jitem.Lookaheads[k] {
				return true
			}
		}

		return false
	})
}

// equals takes two item sets (states) and determines whether or not they are
// equal (NOTE: all sets should be sorted before they are compared to ensure
// they are in the same order - if they are not, this function will fail!).  I
// chose to require them to be in order for efficiency (otherwise this function
// is O(n^2) time complexity every call which is worse than sorting once and
// having linear complexity on every compare - small but valuable optimization).
func equals(a, b []*LRItem) bool {
	if len(a) == len(b) {
		for i, aitem := range a {
			if !reflect.DeepEqual(aitem, b[i]) {
				return false
			}
		}
	}

	return true
}

// lunion takes two sets of a look-aheads and produces their union
func lunion(a, b []string) []string {
	for _, bitem := range b {
		shouldInsert := true

		for _, aitem := range a {
			if aitem == bitem {
				shouldInsert = false
				break
			}
		}

		if shouldInsert {
			a = append(a, bitem)
		}
	}

	return a
}

// LRItemSet represents a particular item state (set) and its connections to
// other states.  The construct is meant to be used in a graph.
type LRItemSet struct {
	Items []*LRItem

	// the string is the value of the terminal or nonterminal (ie. IDENTIFIER,
	// expr, etc.)
	Conns map[string]int
}

// TableGenerator is a construct that stores the shared state of the LALR(1)
// parsing table (AGT) generator.
type TableGenerator struct {
	Sets  []*LRItemSet
	Table *ActionGotoTable
	SG    SimplifiedGrammar
}

// collectSets creates the state graph for the given simplified grammar. This
// operation represents the collection of LR(1) items for the parsing table.
func (tg *TableGenerator) collectSets() {
	// create our initial item set (implicitly augmenting grammar)
	initialSet := []*LRItem{
		&LRItem{
			Name:       _augmGoalSymbol,
			Rule:       []*SGElement{newSGNonterminal(_goalSymbol)},
			DotPos:     0,
			Lookaheads: []string{"$$"},
		},
	}

	// take the closure of our initial item set and make it state 0 (I0)
	initialSet = tg.closureOf(initialSet)

	// put our initial set in order before it is added to the graph
	order(initialSet)

	// add it our starting state to the graph so we can begin construction
	tg.Sets = append(tg.Sets, &LRItemSet{Items: initialSet, Conns: make(map[string]int)})

	// begin constructing the state graph with our initial set
	tg.nextSets(tg.Sets[0])
}

// nextSets takes in a starting set and computes all of the valid sets that
// follow it and then adds them if they don't already exist.  It then creates
// connections to the appropriate states in the graph of all states and then
// calls nextSets recursively on all of the newly produced states so that one
// call from the initial set fully constructs the graph.  It expects an already
// created and added LRItemSet
func (tg *TableGenerator) nextSets(startingSet *LRItemSet) {
	// go through each item and examine the element that the dot is pointing to
	for _, item := range startingSet.Items {
		// if the dot is at the end of the rule, then there are no more
		// connections to create.  Moreover, since epsilons only appear in
		// isolated rules, if we have an epsilon rule, it is equivalent to a
		// rule with the dot at the end and so we can skip it
		if item.DotPos == len(item.Rule) || item.Rule[item.DotPos].Kind == SGEEpsilon {
			continue
		}

		// get the element at the dot (used repeatedly)
		dottedElem := item.Rule[item.DotPos]

		// if we have already calculated the connections for this element, skip it
		if _, ok := startingSet.Conns[dottedElem.Value]; ok {
			continue
		}

		// otherwise compute the GOTO of the item set and determine whether or
		// not to add it to the graph.  NOTE: the goto logic handles epsilons so
		// we don't need to check for them here.
		gotoSet := tg.gotoOf(startingSet.Items, dottedElem)

		// if the goto set is empty, then we have reached the end of the current
		// item (most likely encountered epsilons all the way to the end)
		if len(gotoSet) == 0 {
			continue
		}

		// order and compare out goto set to see if have already calculated its state
		order(gotoSet)

		gotoMatched := false
		for i, set := range tg.Sets {
			if equals(set.Items, gotoSet) {
				// if we have already calculated the state, create a connection
				// back to that precreated state, set the matched flag, and exit
				// the loop (b/c we don't need to search anymore)
				gotoMatched = true
				startingSet.Conns[dottedElem.Value] = i
				break
			}
		}

		// if we have matched, then the connection is already added as is the
		// state so we don't need to add it. (we can skip this last stage)
		if !gotoMatched {
			// add the set to our graph along with a new connection map
			lrGotoSet := &LRItemSet{Items: gotoSet, Conns: make(map[string]int)}
			tg.Sets = append(tg.Sets, lrGotoSet)

			// recursively progress for the goto state (building up states)
			tg.nextSets(lrGotoSet)
		}
	}
}

// gotoOf calculates the new item set expected when the given item set
// encounters the element (NOT EPSILON) provided in the argument.  NOTE: returns
// a new set AND expects that the input item set contains no items where the dot
// is at the end (should be filtered out by larger construction logic).
func (tg *TableGenerator) gotoOf(itemSet []*LRItem, element *SGElement) []*LRItem {
	var newSet []*LRItem

	for _, item := range itemSet {
		// only calculate goto if the element on rules that match the element
		if item.Rule[item.DotPos] == element {
			// create a temporary copy of the item (may be discarded after)
			newItem := *item

			// move the dot forward unconditionally
			newItem.DotPos++

			// NOTE: in both of the following cases, the lookaheads of the
			// original items with dots moved remain the same (only change on
			// closure)

			// if the dotPos is now at the end of the rule, we do not need to
			// calculate the closure of the rule: we can simply add it in its
			// new form
			if newItem.DotPos == len(item.Rule) {
				newSet = addItem(newSet, &newItem)
			} else {
				// we need to calculate the closure since we are not at the end
				// yet so we first create a new item set with just the item we
				// want to close over and the add its closure to the new set
				singleItemSet := []*LRItem{&newItem}
				newSet = union(newSet, tg.closureOf(singleItemSet))
			}
		}
	}

	// all necessary closures have already been applied (no need to call again)
	return newSet
}

// closureOf calculates the closure of a given a item set as well as the
// associated look-aheads of all the new elements.  It returns a new item set.
func (tg *TableGenerator) closureOf(itemSet []*LRItem) []*LRItem {
	for _, item := range itemSet {
		dottedElem := item.Rule[item.DotPos]

		// if we had a nonterminal, we need to close over its items.  Otherwise,
		// we don't care (no sets need to be added)
		if dottedElem.Kind == SGENonterminal {
			var lookaheads []string
			// if the dot will be moved to the end of an item set then, we use
			// its lookaheads instead of the firsts of the symbol after its dot
			if item.DotPos == len(item.Rule)-1 {
				lookaheads = item.Lookaheads
			} else {
				// start by finding the base first set of the following rule
				lookaheads = tg.first(item.Rule[item.DotPos+1:])

				// remove all epsilons from the first set (mutably - first
				// creates a new array ever time so we need to copy)
				n := 0
				for _, f := range lookaheads {
					if f != "" {
						lookaheads[n] = f
						n++
					}
				}

				// if any epsilons were removed (ie, the length changed), then
				// we need to add the lookaheads of our item to the lookaheads
				// of all the new items (we don't need to unconditionally slice
				// since if the length didn't change, there is nothing to slice)
				if n != len(lookaheads) {
					lookaheads = lookaheads[:n]
					lookaheads = lunion(lookaheads, item.Lookaheads)
				}
			}

			// get the new rules to be added as part of the items
			newRules := tg.SG[dottedElem.Value]

			// allocate a base buffer for all the new items (don't actually care
			// about rule contents since they are being added as items in order)
			newItems := make([]*LRItem, len(newRules))

			// add the starting set of new items to the base list using the
			// calculated lookaheads and with the dot at the start of the rule
			for i := range newItems {
				newItems[i] = &LRItem{Name: dottedElem.Value, Rule: newRules[i], DotPos: 0, Lookaheads: lookaheads}
			}

			// calculate the closure of the new item set (recursively)
			newItems = tg.closureOf(newItems)

			// add the new items to the base item set (closure already applied)
			itemSet = union(itemSet, newItems)
		}
	}

	return itemSet
}

// first finds the first set of a given *rule*. it is intended to be used
// recursively meaning it will accept slices of rules (ie. it simply finds the
// firsts of the set of elements it is given) - used in finding closure
func (tg *TableGenerator) first(rule []*SGElement) []string {
	if rule[0].Kind == SGENonterminal {
		var firstSet []string

		// accumulate all of the firsts of the nonterminal before applying
		// additional first calculation logic
		for _, r := range tg.SG[rule[0].Value] {
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
