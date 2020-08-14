package types

// Const status refers to constancy status is used to indicate how "constant" a
// given type.  There are three levels: constant -- the type is known or defined
// to be a true constant; unknown - the type may or may not be constant; and
// mutable -- the type is known to be mutable (guaranteed). These levels are
// used to faciliate inferred constancy.  Note that some types may have their
// own definition or interpretation of "constant" (eg. functions are constant if
// they don't mutate their enclosing scopes, references are constant if their
// underlying type is constant).
const (
	CSConstant = iota
	CSUnknown
	CSMutable
)

// CompareConstStatus indicates that two different constancy statuses are equivalent.
func CompareConstStatus(a, b int) bool {
	if a == b {
		return true
	}

	if a == CSUnknown || b == CSUnknown {
		return true
	}

	return false
}
