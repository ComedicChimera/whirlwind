package syntax

// a grammar is defined to be a set of named productions
type Grammar map[string]Production

// a production itself is simply a set of grammatical elements
type Production []GrammaticalElement

const (
	GKIND_ALTERNATOR = iota
	GKIND_REPEAT
	GKIND_REPEAT_MULTIPLE
	GKIND_GROUP
	GKIND_OPTIONAL
	GKIND_TERMINAL
	GKIND_NONTERMINAL
)

// there are many kinds of grammatical elements as
// enumerated above that this interface accounts
// for, meant to be used as a check before a type assert
type GrammaticalElement interface {
	Kind() int
}

// Terminal grammatical element (rename of string)
type Terminal string

// Nonterminal grammatical element (rename of string)
type Nonterminal string

// GroupingElement represents all of the other grouping
// grammatical elements (eg. groups, optionals, repeats, etc.)
type GroupingElement struct {
	kind     int
	elements []GrammaticalElement
}

// NewGroupingElement creates a new grouping element
func NewGroupingElement(kind int, elems []GrammaticalElement) GroupingElement {
	return GroupingElement{kind: kind, elements: elems}
}

// return the known Kind of the two singular grammatical elements
func (Terminal) Kind() int {
	return GKIND_TERMINAL
}

func (Nonterminal) Kind() int {
	return GKIND_NONTERMINAL
}

// return the kind of grouping elements
// based on the stored kind member variable
func (g GroupingElement) Kind() int {
	return g.kind
}

// represents a grammatical alternator storing
// a slice of the subgroups it alternates between
type AlternatorElement struct {
	groups [][]GrammaticalElement
}

// create a new alternator element from some number of groups efficiently
func NewAlternatorElement(groups ...[]GrammaticalElement) AlternatorElement {
	return AlternatorElement{groups: groups}
}

// known kind implementation
func (AlternatorElement) Kind() int {
	return GKIND_ALTERNATOR
}

// pushes a group onto the front of alternator element
func (ae *AlternatorElement) PushFront(group []GrammaticalElement) {
	ae.groups = append([][]GrammaticalElement{group}, ae.groups...)
}
