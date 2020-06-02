package syntax

import (
	"fmt"
	"strings"
)

// SimplifiedGrammar represents the desugared and simplified grammar (converted
// from an EBNF-based representation to a BNF-based representation). It acts as
// an intermediary between the grammar and the action-goto table.
type SimplifiedGrammar map[string]SimplifiedProduction

// SimplifiedProduction represents a production in the simplified grammar
type SimplifiedProduction [][]*SGElement

// SGElement represents a single element of the action-goto table whose kind is
// characterized by the constants prefixed SGE (terminal, nonterminal, epsilon)
type SGElement struct {
	Kind  int
	Value string
}

// represent the different kinds of simplified grammatical elements
const (
	SGENonterminal = iota
	SGETerminal
	SGEEpsilon
)

// Simplifier is the mechanism used to generate the simplifed grammar from
// the base, more complex grammar.  It stores several p
type Simplifier struct {
	SGrammar SimplifiedGrammar

	src Grammar

	// used to generate names for the anonymous productions
	anonCounter int

	// allows us to avoid unnecessarily recalculating follows
	followTable map[string][]string

	// store the current, named production being evaluated so that anonymous
	// productions can indicate which production they are apart of
	currNamedProduction string
}

// create some reusable methods to build the action-goto table
func newSGNonterminal(name string) *SGElement {
	return &SGElement{Kind: SGENonterminal, Value: name}
}

func epsilonRule() []*SGElement {
	return []*SGElement{&SGElement{Kind: SGEEpsilon}}
}

// reducing the grammar means removing all of the syntactic sugar in the
// grammar. This is accomplished by splitting the grammar into anonymous
// productions that will not be compiled into the main tree but can allow us to
// avoid unnecessary complexity when creating the action-goto table.  notably,
// this simplification also introduces epsilons to handle optional patterns
// (which is expected by the larger parser generator)
func simplifyGrammar(g Grammar) SimplifiedGrammar {
	// create a basic simplified grammar from the global goal symbol (and
	// allocate all of the necessary data structures)
	s := &Simplifier{SGrammar: make(map[string]SimplifiedProduction), src: g,
		followTable: make(map[string][]string),
	}

	// simplify each production in the grammar
	for k, v := range g {
		s.simplifyProduction(k, v)
	}

	// remove the source grammar from the simplified grammar so it can be
	// garbage-collected when the function to create the grammar closes
	s.src = nil

	// return the simplified production
	return s.SGrammar
}

// this function handles the high level aspects of the productions ie.
// alternators and addition to the simplified grammar the lower level group
// simplification is done by another function. note that this function works for
// anonymous productions as well as named ones
func (s *Simplifier) simplifyProduction(name string, p Production) {
	var sp SimplifiedProduction

	// if we are dealing with named production, update the simplifier
	if !strings.HasPrefix(name, "$") {
		s.currNamedProduction = name
	}

	// split alternators into rules (see action-goto table gen algo description
	// for "definition" of a rule).  Alternators are always alone when generated.
	if p[0].Kind() == GKindAlternator {
		alternator := p[0].(AlternatorElement)

		sp = make(SimplifiedProduction, len(alternator.groups))

		for i, g := range alternator.groups {
			sp[i] = s.simplifyGroup(g)
		}
		// otherwise create a single rule for the entire production
	} else {
		sp = [][]*SGElement{s.simplifyGroup(p)}
	}

	s.SGrammar[name] = sp
}

// converts a group (or a rule in a production) into a simplified form (ie. only
// terminals, nonterminals and epsilon, could be thought of a "desugaring")
func (s *Simplifier) simplifyGroup(elems []GrammaticalElement) []*SGElement {
	// group will always be the same size as the original
	group := make([]*SGElement, len(elems))

	for i, item := range elems {
		// terminals and non terminals are essentially added as is (just
		// converted to SGElements which doesn't actually change their behavior)
		switch item.Kind() {
		case GKindTerminal:
			group[i] = &SGElement{Kind: SGETerminal, Value: string(item.(Terminal))}
		case GKindNonterminal:
			group[i] = newSGNonterminal(string(item.(Nonterminal)))
		// groups are made into anonymous productions and inserted as
		// nonterminals unless it is determined that the group contains only one
		// simplified grammatical element in which case it is inlined
		case GKindGroup:
			sgroup := s.simplifyGroup(item.(GroupingElement).elements)

			if len(sgroup) == 1 {
				group[i] = sgroup[0]
			} else {
				gAnonName := s.createAnonName()
				s.SGrammar[gAnonName] = [][]*SGElement{sgroup}
				group[i] = newSGNonterminal(gAnonName)
			}
		// similar idea to groups; however an anonymous production is always created
		// and an epsilon rule is added to newly created anonymous production
		case GKindOptional:
			group[i] = newSGNonterminal(
				s.insertAnonProduction(item.(GroupingElement).elements, true),
			)
		// 0+ repeats create a single anonymous production of the form with two
		// rules: the contents of the repeat element + a nonterminal reference
		// to the new production and an epsilon rule.  This produces the desired
		// repition behavior
		case GKindRepeat:
			baseGroup := s.simplifyGroup(item.(GroupingElement).elements)

			rAnonName := s.createAnonName()
			rnt := newSGNonterminal(rAnonName)

			s.SGrammar[rAnonName] = [][]*SGElement{
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
			prodName := s.insertAnonProduction(item.(GroupingElement).elements, false)

			rAnonName := s.createAnonName()
			rnt := newSGNonterminal(rAnonName)

			s.SGrammar[rAnonName] = [][]*SGElement{
				[]*SGElement{newSGNonterminal(prodName), rnt},
				epsilonRule(),
			}

			rmAnonName := s.createAnonName()

			s.SGrammar[rmAnonName] = [][]*SGElement{
				[]*SGElement{newSGNonterminal(prodName), rnt},
			}

			group[i] = newSGNonterminal(rmAnonName)
		// occassionally, alternators can show up as raw groups (due to
		// grammatical simplification: attempting to simplify number of groups)
		// so we need handle them which is fairly simple: just create a new
		// anonymous production for them (simplifyProduction will handle rest)
		case GKindAlternator:
			prodName := s.insertAnonProduction([]GrammaticalElement{item}, false)

			group[i] = newSGNonterminal(prodName)
		}
	}

	return group
}

// utility functions for created the simplified grammar
func (s *Simplifier) insertAnonProduction(elements []GrammaticalElement, hasEpsilon bool) string {
	anonName := s.createAnonName()

	s.simplifyProduction(anonName, elements)

	// hasEpsilon means that an epsilon rule will be appended at the end of the
	// new anonymous production
	if hasEpsilon {
		s.SGrammar[anonName] = append(s.SGrammar[anonName], epsilonRule())
	}

	return anonName
}

// note that this function returns a new name every time it is called ie. it is
// not an accessor for the the anonCounter
func (s *Simplifier) createAnonName() string {
	anonName := fmt.Sprintf("$%d-%s", s.anonCounter, s.currNamedProduction)
	s.anonCounter++
	return anonName
}
