package syntax

import (
	"errors"
	"fmt"
	"strings"
)

// represent the different kinds of parsing table elements (PTR = parsing table
// feature)
const (
	PTFNonterminal = iota
	PTFTerminal
	PTFEpsilon
)

// store the global start symbol of the grammar
const _startSymbol = "whirlwind"

// ParsingTable represents an LL(1) parsing table that is implemented as a table
// where the row keys are the names of productions and the column keys are the
// names of tokens and the contents are rules
type ParsingTable map[string]map[string][]*PTableElement

// PTableElement represents a single element of the parsing table whose kind is
// characterized by the constants prefixed PTF (terminal, nonterminal, epsilon)
type PTableElement struct {
	Kind  int
	Value string
}

// create some reusable methods to build the parsing table
func newPTableNonterminal(name string) *PTableElement {
	return &PTableElement{Kind: PTFNonterminal, Value: name}
}

func epsilonRule() []*PTableElement {
	return []*PTableElement{&PTableElement{Kind: PTFEpsilon}}
}

// convert the grammar into a parsing table (meant to be called once at the
// start of the compiler)
func createParsingTable(g Grammar) (ParsingTable, error) {
	// first, we need to "reduce" the grammar (read the part about grammatical
	// reduction to understand why)
	rg := reduceGrammar(g)

	// allocate the parsing table (empty by default)
	table := make(ParsingTable)

	// iterate through each production in the reduced grammar and create parsing
	// table entries for it
	for name, prod := range rg.Productions {
		table[name] = make(map[string][]*PTableElement)

		// go through each "rule" in the production where a rule is essentially
		// an alternated group of a production.  For example, if you had the
		// production `A | B | C`, the rules would be A, B, and C respectively.
		for _, rule := range prod {
			// this *should* never happen. mainly here just to check the
			// integrity of the grammar during development. a bug filter if you
			// will.
			if len(rule) == 0 {
				return nil, errors.New("Empty grammatical rule in production '" + name + "'")
			}

			// we are using a standard LL(1) parser generation algorithm at this
			// point. see the wikipedia entry as my implementation essentially
			// follows the description given here (based on the formal
			// definition, not the examples):
			// https://en.wikipedia.org/wiki/LL_parser#Constructing_an_LL(1)_parsing_table

			// a couple notes: - the empty string represents an epsilon (should
			// never occur as a valid token) - the set of firsts *should* at
			// most only contain one epsilon (we rely on that) - this section
			// will return errors if the grammar is determined to be ambiguous

			// the actual algorithm implementation for building the parsing
			// table is as follows: we iterate through each of the firsts, and
			// if no epsilons are detected, we simply add them and their
			// associated rules as is to the parsing table. if there is an
			// epsilon, we replace that epsilon's entry in the parsing table
			// with those of the follows of the production. note that the
			// epsilon is NOT added (as one would expect)
			firstSet := rg.first(rule)
			for _, first := range firstSet {
				if first == "" {
					for _, follow := range rg.follow(name) {
						if _, ok := table[name][follow]; ok {
							return nil, fmt.Errorf("Grammar Error: Ambiguous terminal `%s` following `%s`", follow, name)
						}

						table[name][follow] = rule
					}
				} else {
					if _, ok := table[name][first]; ok {
						return nil, fmt.Errorf("Grammar Error: Ambiguous terminal `%s` in `%s`", first, name)
					}

					table[name][first] = rule
				}
			}
		}
	}

	return table, nil
}

// reducing the grammar means removing all of the syntactic sugar in the
// grammar. This is accomplished by splitting the grammar into anonymous
// productions that will not be compiled into the main tree but can allow us to
// avoid unnecessary complexity when creating the parsing table.  notably, this
// reduction also introduces epsilons to handle optional patterns (which is
// expected by the larger parser generator)
func reduceGrammar(g Grammar) *ReducedGrammar {
	// create a basic reduced grammar from the global start symbol (and allocate
	// all of the necessary data structures)
	rg := &ReducedGrammar{Productions: make(map[string]ReducedProduction), src: g,
		startSymbol: _startSymbol, followTable: make(map[string][]string),
	}

	// reduce each production in the grammar
	for k, v := range g {
		rg.reduceProduction(k, v)
	}

	// return the reduced production
	return rg
}

// ReducedGrammar represents the reduced grammar (extracted from the more
// complex grammar). notably, this structure also contains several methods and
// data structures that help in the creation of the parsing table (determining
// firsts and follows). it acts as an intermediary between the grammar and the
// parsing table
type ReducedGrammar struct {
	Productions map[string]ReducedProduction

	src         Grammar
	startSymbol string

	// used to generate names for the anonymous productions
	anonCounter int

	// allows us to avoid unnecessarily recalculating follows
	followTable map[string][]string

	// store the current, named production being evaluated so that anonymous
	// productions can indicate which production they are apart of
	currNamedProduction string
}

// ReducedProduction represents a production in the reduced grammar
type ReducedProduction [][]*PTableElement

// this function handles the high level aspects of the productions ie.
// alternators and addition to the reduced grammar the lower level group
// reduction is done by another function. note that this function works for
// anonymous productions as well as named ones
func (rg *ReducedGrammar) reduceProduction(name string, p Production) {
	var rp ReducedProduction

	// if we are dealing with named production, update the reducer
	if !strings.HasPrefix(name, "$") {
		rg.currNamedProduction = name
	}

	// split alternators into rules (see parsing table gen algo description for
	// "definition" of a rule)
	if p[0].Kind() == GKindAlternator {
		alternator := p[0].(AlternatorElement)

		rp = make(ReducedProduction, len(alternator.groups))

		for i, g := range alternator.groups {
			rp[i] = rg.reduceGroup(g)
		}
		// otherwise create a single rule for the entire production
	} else {
		rp = [][]*PTableElement{rg.reduceGroup(p)}
	}

	rg.Productions[name] = rp
}

// converts a group (or a rule in a production) into a reduced form (ie. only
// terminals, nonterminals and epsilon, could be thought of a "desugaring")
func (rg *ReducedGrammar) reduceGroup(elems []GrammaticalElement) []*PTableElement {
	group := make([]*PTableElement, len(elems))

	for i, item := range elems {
		// terminals and non terminals are essentially added as is (just
		// converted to PTableElements which doesn't actually change their
		// behavior)
		switch item.Kind() {
		case GKindTerminal:
			group[i] = &PTableElement{Kind: PTFTerminal, Value: string(item.(Terminal))}
		case GKindNonterminal:
			group[i] = newPTableNonterminal(string(item.(Nonterminal)))
		// groups are made into anonymous productions and inserted as
		// nonterminals
		case GKindGroup:
			group[i] = newPTableNonterminal(
				rg.insertAnonProduction(item.(GroupingElement).elements, false),
			)
		// same deals as groups, but with an epsilon rule included
		case GKindOptional:
			group[i] = newPTableNonterminal(
				rg.insertAnonProduction(item.(GroupingElement).elements, true),
			)
		// 0+ repeats create a single anonymous production of the form with two
		// rules: the contents of the repeat element + a nonterminal reference
		// to the new production and an epsilon rule.  This produces the desired
		// repition behavior
		case GKindRepeat:
			baseGroup := rg.reduceGroup(item.(GroupingElement).elements)

			rAnonName := rg.createAnonName()
			rnt := newPTableNonterminal(rAnonName)

			rg.Productions[rAnonName] = [][]*PTableElement{
				append(baseGroup, rnt),
				epsilonRule(),
			}

			group[i] = rnt
		// 1+ repeats first lower the original contents of the element into
		// their own anonymous production.  Then, they create an anonymous
		// production of a form identical to that of a 0+ repeat but with a
		// reference to first anonymous production (the one holding the original
		// content) instead of the original content of the production. Finally,
		// they create one new anonymous production that begins with a reference
		// to the original production followed by a reference to the 0+ repeat
		// production.  They then insert a reference to this final production
		// into the current production. (note: the reason for not inlining the
		// final production is to allow the size of the initial production to
		// predictable - avoid time loss of a resize)
		case GKindRepeatMultiple:
			prodName := rg.insertAnonProduction(item.(GroupingElement).elements, false)

			rAnonName := rg.createAnonName()
			rnt := newPTableNonterminal(rAnonName)

			rg.Productions[rAnonName] = [][]*PTableElement{
				[]*PTableElement{newPTableNonterminal(prodName), rnt},
				epsilonRule(),
			}

			rmAnonName := rg.createAnonName()

			rg.Productions[rmAnonName] = [][]*PTableElement{
				[]*PTableElement{newPTableNonterminal(prodName), rnt},
			}

			group[i] = newPTableNonterminal(rmAnonName)
		// occassionally, alternators can show up as raw groups (due to
		// grammatical simplification: attempting to reduce number of groups) so
		// we need handle them which is fairly simple: just create a new
		// anonymous production for them (reduceProduction will handle rest)
		case GKindAlternator:
			prodName := rg.insertAnonProduction([]GrammaticalElement{item}, false)

			group[i] = newPTableNonterminal(prodName)
		}
	}

	// return the newly build group (should be same size at the original)
	return group
}

// utility functions for created the reduced grammar
func (rg *ReducedGrammar) insertAnonProduction(elements []GrammaticalElement, hasEpsilon bool) string {
	anonName := rg.createAnonName()

	rg.reduceProduction(anonName, elements)

	// hasEpsilon means that an epsilon rule will be appended at the end of the
	// new anonymous production
	if hasEpsilon {
		rg.Productions[anonName] = append(rg.Productions[anonName], epsilonRule())
	}

	return anonName
}

// note that this function returns a new name every time it is called ie. it is
// not an accessor for the the anonCounter
func (rg *ReducedGrammar) createAnonName() string {
	anonName := fmt.Sprintf("$%d-%s", rg.anonCounter, rg.currNamedProduction)
	rg.anonCounter++
	return anonName
}

// the following two functions are used to create the parsing table from the
// reduced grammar and behave as their names would imply

// finds the firsts of a given *rule*. it is intended to be used recursively
// meaning it will accept slices of rules (ie. it simply finds the firsts of the
// set of elements it is given)
func (rg *ReducedGrammar) first(rule []*PTableElement) []string {
	if rule[0].Kind == PTFNonterminal {
		var firstSet []string

		// accumulate all of the firsts of the nonterminal before applying
		// additional first calculation logic
		for _, r := range rg.Productions[rule[0].Value] {
			ntFirst := rg.first(r)

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
		// valid firsts for the rule (ie. Fi(Aw') = Fi(A) \ { epsilon } U Fi(w'))
		if n != len(firstSet) {
			firstSet = firstSet[:n]
			firstSet = append(firstSet, rg.first(rule[1:])...)
		}

		return firstSet
	}

	// catches both terminals and epsilon since Fi(epsilon) = { epsilon } and
	// Fi(a) where a is a terminal = { a }
	return []string{rule[0].Value}
}

// follow finds all of the follows of given production where "symbol" is the
// name of said production in the reduced grammar
func (rg *ReducedGrammar) follow(symbol string) []string {
	// if we have already evaluated the follows of this symbol we return what we
	// have (since follow is an expensive operaton and unlike firsts is based
	//  on the name as opposed to a rule list)
	if f, ok := rg.followTable[symbol]; ok {
		if f == nil {
			// fmt.Println("Caught Error?")
			return []string{}
		}

		return f
	}

	rg.followTable[symbol] = nil

	var followSet []string

	// the follow set of the start symbol contains a special token representing
	// the end of the token stream ("$$") not be confused with token "$" used
	// for macros :)
	if symbol == rg.startSymbol {
		followSet = []string{"$$"}
	}

	for name, prod := range rg.Productions {
		for _, rule := range prod {
			// we use this boolean as a flag to tell us whether or not we have
			// encountered a reference to our symbol's production in the current
			// rule. note that we might not encounter any such reference
			takingFollows := false

			// iterate through the rule until we encounter a reference to our
			// symbol or until we have exhausted the rule. if we encounter a
			// reference then we begin taking follows from the following symbol.
			// if not, we simply finish iteration and move on to the next rule
			for i, item := range rule {
				// if we are taking follows, then we continue taking follows
				// until we encounter a first set that does not contain and
				// epsilon at which point we exit out.  if we encounter a first
				// set that contains nothing, we add the follows of the current
				// production the the follows of our symbol's production
				if takingFollows {
					firstSet := rg.first(rule[i:])

					if len(firstSet) == 0 {
						followSet = append(followSet, rg.follow(name)...)
						break
					}

					// remove any epsilons from the first set
					n := 0
					for _, first := range firstSet {
						if first != "" {
							firstSet[n] = first
							n++
						}
					}

					// we want to keep the length of the first set so we know
					// whether or not to exit so we don't modify the length of
					// the first set here and just slice in the argument
					followSet = append(followSet, firstSet[:n]...)

					// if filtering for epsilons did not change the length, then
					// there was no epsilon
					if n == len(firstSet) {
						// clear taking follows before we break so that we don't
						// collect follows of the current production
						takingFollows = false
						break
					}
				} else if item.Kind == PTFNonterminal && item.Value == symbol {
					takingFollows = true
				}
			}

			// if we are still taking follows at this point, then we know we
			// reached the end of the rule which means that the we also need to
			// take into account the follows of the current production.
			// However, if we are actually searching the production that our
			// symbol links to, then we don't collect follows because we are
			// already doing that (prevent infinite recursion :D)
			if takingFollows && name != symbol {
				followSet = append(followSet, rg.follow(name)...)
			}
		}
	}

	// remove duplicates from follow set (it is a set after all :D)
	followHT := make(map[string]struct{})
	for _, item := range followSet {
		followHT[item] = struct{}{}
	}

	followSet = followSet[:len(followHT)]

	i := 0
	for k := range followHT {
		followSet[i] = k
		i++
	}

	// create an entry for any follow sets that weren't already determined
	rg.followTable[symbol] = followSet

	return followSet
}
