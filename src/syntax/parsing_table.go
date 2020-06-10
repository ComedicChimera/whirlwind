package syntax

// ParsingTable represents our adapted LALR(1) parser's Action-Goto table as
// well as all of the rules it can reduce by
type ParsingTable struct {
	Rows  []*PTableRow
	Rules []*PTableRule
}

// PTableRow is a particular row in the parsing table.  Any terminal for which
// there is no key in the action table is considered unexpected (unless it is
// whitespace in which case some special rules can apply as necessary)
type PTableRow struct {
	Actions map[int]*Action
	Gotos   map[string]int
}

// Action contains two items: a kind and an operand.  The kind indicates what
// type of action to perform (Shift, Reduce, Accept) and the operand is used to
// store any data affiliated with the action (state to shift to for shift
// actions, rule to reduce by for reduce actions, nothing for accept actions)
type Action struct {
	// Kind should one of the action kinds enumerated below (prefix AK)
	Kind int

	Operand int
}

// Three different kinds of valid actions (that can be explicitly included)
const (
	AKReduce = iota
	AKShift
	AKAccept
)

// PTableRule is used to represent a given reduction pattern.  Note that since
// the actual elements of a rule are not useful at run time, we simply store the
// number of items to take into the new tree and its name.  This also allows to
// reduce (haha get it?) the number of rules needed to perform an accurate parse.
type PTableRule struct {
	Name  string
	Count int
}
