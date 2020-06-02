package syntax

const _goalSymbol = "whirlwind"
const _augmGoalSymbol = "_end_"

// ActionGotoTable represents the action-goto table of our LALR(1) parser
// along with all of the associated rules (stored after being extracted
// from the SimplifiedGrammar which is disposed as its data is extracted)
type ActionGotoTable struct {
	Rows  []*AGTRow
	Rules []*AGTRule
}

// AGTRow represents a row in the action-goto table
type AGTRow struct {
	Actions map[string]*Action
	Gotos   map[string]string
}

// AGTRule represents a particular rule for the LALR(1) parser.
// It does not store what elements to take only how many as for
// the purposes of using the rule, the actual contents don't
// matter (already checked by parser if reduce is called).
type AGTRule struct {
	Name string
	Take int
}

// Action represents an action in the action-goto table.
type Action struct {
	// Kind must be one of the valid action kinds (listed below)
	Kind int

	// Data is any data the action holds (shift state, reduce rule)
	Data int
}

// Valid action kinds
const (
	AKShift = iota
	AKReduce
	AKDone // stores no data
)
