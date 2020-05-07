package syntax

// Grammar is defined to be a set of named productions
type Grammar map[string]Production

// Production itself is simply a set of grammatical elements
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

// GrammaticalElement represents a piece of the grammar
// once it is serialized into an object
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

// Kind of a terminal is GKIND_TERMINAL
func (Terminal) Kind() int {
	return GKIND_TERMINAL
}

// Kind of a nonterminal is GKIND_NONTERMINAL
func (Nonterminal) Kind() int {
	return GKIND_NONTERMINAL
}

// Kind returns the kind of grouping elements
// based on the stored kind member variable
func (g GroupingElement) Kind() int {
	return g.kind
}

// AlternatorElementsrepresents a grammatical alternator
// storing a slice of the subgroups it alternates between
type AlternatorElement struct {
	groups [][]GrammaticalElement
}

// NewAlternatorElement create a new alternator element from some number of groups efficiently
func NewAlternatorElement(groups ...[]GrammaticalElement) AlternatorElement {
	return AlternatorElement{groups: groups}
}

// Kind of alternator is GKIND_ALTERNATOR
func (AlternatorElement) Kind() int {
	return GKIND_ALTERNATOR
}

// PushFront pushes a group onto the front of alternator element
func (ae *AlternatorElement) PushFront(group []GrammaticalElement) {
	ae.groups = append([][]GrammaticalElement{group}, ae.groups...)
}
