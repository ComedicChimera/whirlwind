package typing

// BindingRegistry is a data structure for storing and accessing bindings to the
// various data types.  It can exist at a file (local) level or at a package
// (global) level.
type BindingRegistry struct {
	Bindings []*Binding
}

// Binding represents a single binding of a type interface to a given type
type Binding struct {
	// TODO: matching stuff

	TypeInterf *InterfType
	Exported   bool
}

// PrivateCopy creates a new copy of a binding that is no longer exported
func (b *Binding) PrivateCopy() *Binding {
	return &Binding{
		TypeInterf: b.TypeInterf,
		Exported:   false,
	}
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
