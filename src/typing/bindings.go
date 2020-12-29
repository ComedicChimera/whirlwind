package typing

// BindingRegistry is a data structure for storing and accessing bindings to the
// various data types.  It can exist at a file (local) level or at a package
// (global) level.
type BindingRegistry struct {
	Bindings []*Binding
}

// Binding represents a single binding of a type interface to a given type
type Binding struct {
	// MatchType is a DataType object used the match any given input type
	// to this binding.  If an input type is equal to this field, then the
	// Binding is a match.  This works both for generic bindings and regular
	// bindings since generic bindings will simply contain WildcardTypes in
	// these fields that will be equal to anything that satisfies their
	// restrictors (they won't have any given value and will be separate from).
	MatchType DataType

	// Wildcards is a slice of all the Wildcard types used in the binding
	Wildcards []*WildcardType

	TypeInterf DataType
	Exported   bool
}

// PrivateCopy creates a new copy of a binding that is no longer exported
func (b *Binding) PrivateCopy() *Binding {
	return &Binding{
		TypeInterf: b.TypeInterf,
		Exported:   false,
	}
}

// GetBindings fetches all applicable interface bindings for a given type
func (s *Solver) GetBindings(br *BindingRegistry, dt DataType) []*InterfType {
	var matches []*InterfType

	for _, binding := range br.Bindings {
		if binding.MatchType.Equals(dt) {
			switch v := binding.TypeInterf.(type) {
			case *InterfType:
				// no wildcards to clear here since this is not a generic binding
				matches = append(matches, v)
			case *GenericType:
				typeValues := make([]DataType, len(binding.Wildcards))

				// the compiler MUST check that all wildcards are satisfied when
				// the binding is created (that is all wildcards can be
				// determined on match)
				for i, wc := range binding.Wildcards {
					typeValues[i] = wc.Value

					// we need to clear the value for subsequent bindings
					wc.Value = nil
				}

				// should always succeed if the binding was created properly
				gi, _ := s.CreateGenericInstance(v, typeValues)
				matches = append(matches, gi.(*InterfType))
			}
		}
	}

	return matches
}

// ImplementsInterf tests if a given type implements the given interface. The
// interface should NOT be a type interface.
func (s *Solver) ImplementsInterf(dt DataType, it *InterfType) bool {
	// Interfaces can never implement other interfaces
	if _, ok := dt.(*InterfType); ok {
		return false
	}

	if ContainsType(dt, it.Instances) {
		return true
	}

	localBindings := s.GetBindings(s.LocalBindings, dt)
	globalBindings := s.GetBindings(s.GlobalBindings, dt)

outerloop:
	for name, method := range it.Methods {
		if method.Kind == MKAbstract {
			for _, binding := range localBindings {
				if containsMethod(binding, name, method) {
					continue outerloop
				} else {
					return false
				}
			}

			for _, binding := range globalBindings {
				if containsMethod(binding, name, method) {
					continue outerloop
				} else {
					return false
				}
			}
		}
	}

	// we want to add this type to the list of instances on the interface
	// type as form of memoization (that way we don't have to perform the
	// method lookup multiple times)
	it.Instances = append(it.Instances, dt)

	return true
}

// Derive performs a formal implement (an `is` derivation) on a type interface.
// This should be called after `ImplementsInterf` is called to check the
// derivation.
func (s *Solver) Derive(it, deriving *InterfType) {
	for name, method := range deriving.Methods {
		if imethod, ok := it.Methods[name]; ok {
			// we can assume that if the `ImplementsInterf` check passed, then
			// if two methods have the same name in the parent and child then
			// they have the same signature
			if method.Kind == MKAbstract {
				imethod.Kind = MKImplement
			} else {
				// MKVirtual in parent, override in derived
				imethod.Kind = MKOverride
			}
		} else if method.Kind == MKVirtual {
			it.Methods[name] = method
		}

		// we can assume all abstract methods are implemented (for same reason
		// as other assumption)
	}
}

// containsMethod checks if a given type interface contains a method
func containsMethod(binding *InterfType, methodName string, method *InterfMethod) bool {
	if bmethod, ok := binding.Methods[methodName]; ok {
		return bmethod.Signature.Equals(method.Signature)
	}

	return false
}

// MigrateBindings perform importing/lifting of bindings from another package into
// either the current file or current package (depends on what registry is passed in).
func MigrateBindings(src, dest *BindingRegistry, lift bool) {
	for _, binding := range src.Bindings {
		if binding.Exported {
			if lift {
				dest.Bindings = append(dest.Bindings, binding)
			} else {
				dest.Bindings = append(dest.Bindings, binding.PrivateCopy())
			}
		}
	}
}
