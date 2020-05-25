package types

// Store the four different kinds of methods in any given interface (most useful
// during backend)
const (
	MKVirtual = iota
	MKOverridden
	MKAbstract
	MKImplemented
)

// Method represents a single method an interface May be either a FuncType or a
// GenericType
type Method struct {
	FnType DataType
	Kind   int
}

// TypeInterf represents a set of methods.  Note that it is not a data type by
// itself: rather it is a property of a specific type set or a type aspect bound
// onto a particular type
type TypeInterf struct {
	Methods    map[string]*Method
	Implements []*TypeInterf
}

// AddMethod attempts to add method to a type interface. If a method of the
// given name already exists, then this function fails.  If it does not, the new
// method is added. mk represents the method kind (what type of method is being
// added)
func (ti *TypeInterf) AddMethod(name string, dt DataType, mk int) bool {
	if _, ok := ti.Methods[name]; ok {
		return false
	}

	ti.Methods[name] = &Method{FnType: dt, Kind: mk}
	return true
}

// MatchType attempts to determine whether or not the given data type can be a
// member of the given interface based on its included type interface.  It does
// NOT connote an implementation: it is simply a quantification check.  The
// error returned indicates (if not nil) why the type does not match (for better
// error messages)
func (ti *TypeInterf) MatchType(dt DataType) error {
	return nil
}

// ImplementOn implements the given type interface on the type interface of the
// data type.  This does NOT check whether or not the type can implement this
// interface, it just adds itself as to the binding of the data type in
// appropriate index (local or global).
func (ti *TypeInterf) ImplementOn(dt DataType, global bool) {

}
