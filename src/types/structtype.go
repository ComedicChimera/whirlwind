package types

// StructType represents a named explicitly structured data type
type StructType struct {
	Name    string
	Members map[string]*StructMember
	Packed  bool
}

// StructMember represents a member of a structured type.  It includes certain
// salient value properties of the member for utilities sake and is stored as a
// pointer because it should ideally only be created once per data structure and
// needs to be comparable by value
type StructMember struct {
	Type     DataType
	Constant bool
	Volatile bool
}

// NewStructType creates a new named, structured data type in given package with
// the given members.  Requires a flag to indicate whether or not the struct is
// packed
func NewStructType(srcPkg string, name string, members map[string]*StructMember, packed bool) DataType {
	return &StructType{Name: srcPkg + "::" + name, Members: members, Packed: packed}
}

// struct types don't implement coercion
func (st *StructType) coerce(dt DataType) bool {
	return false
}

func (st *StructType) equals(dt DataType) bool {
	if ost, ok := dt.(*StructType); ok {
		// struct equality is technically based only on name (so this is the
		// only real test for equality, all future elements can assume true
		// equality)
		if st.Name != ost.Name {
			return false
		}

		// we have to satify any free types in the structs so we still need to
		// compare their types (we can ignore all other info tho b/c they are
		// already equal)
		for name, member := range st.Members {
			// we don't care about the result of the member comparison (since
			// structs are already given to be equal from the name comparison
			// above)
			st.safeCompareMember(ost, member, ost.Members[name])
		}
	}

	return false
}

// structs can be cast if their members are identical
func (st *StructType) cast(dt DataType) bool {
	if ost, ok := dt.(*StructType); ok {
		for name, member := range st.Members {
			// TODO: covariant struct casting
			if omember, ok := ost.Members[name]; !ok || !st.safeCompareMember(ost, member, omember) {
				return false
			}
		}

		return true
	}

	return false
}

// Since structs can contain self-referential types (in the form of references),
// we need to make sure we don't start recurring indefinitely whenever we
// compare members that are self-referential.
func (st *StructType) safeCompareMember(ost *StructType, stMember, ostMember *StructMember) bool {
	// Trust me, this if tower makes my eyes bleed as much as it does yours.
	// (Perhaps you now see why Whirlwind has an `is` operator, yes?)
	if rt, ok := stMember.Type.(*ReferenceType); ok {
		if ort, ok := ostMember.Type.(*ReferenceType); ok {
			if rtelemstruct, ok := rt.ElemType.(*StructType); ok {
				if ortelemstruct, ok := ort.ElemType.(*StructType); ok {
					if rtelemstruct.Name == st.Name && ortelemstruct.Name == ost.Name && st.Name != ost.Name {
						return false
					}
				}
			}
		}
	}

	return Equals(stMember.Type, ostMember.Type) && stMember.Volatile == ostMember.Volatile && stMember.Constant == ostMember.Constant
}

// Repr of a named type is simply its name
func (st *StructType) Repr() string {
	return st.Name
}

// SizeOf a structure depends on whether or not it is packed.  If it is packed
// then it is the sum of the individual element sizes.  Otherwise, it is the
// padded size of the data structure (for alignment)
func (st *StructType) SizeOf() uint {
	var packedSize uint = 0

	for _, m := range st.Members {
		packedSize += m.Type.SizeOf()
	}

	if st.Packed {
		return packedSize
	}

	var maxSize uint = 0

	for _, m := range st.Members {
		msize := m.Type.SizeOf()

		if msize > maxSize {
			maxSize = msize
		}
	}

	return SmallestMultiple(packedSize, maxSize)
}

// AlignOf a struct depends on whether or not it is packed. If it is padded,
// then the alignment is the largest alignment of its members.  Otherwise, it is
// (TODO) NOTE: On the backend, structs are more often implemented as pointers
// which means that within data structures, their alignment should be that of a
// pointer (!IMPORTANT)
func (st *StructType) AlignOf() uint {
	if st.Packed {
		// TODO: packed alignment
		return 0
	}

	var maxAlign uint = 0

	for _, m := range st.Members {
		malign := m.Type.AlignOf()

		if malign > maxAlign {
			maxAlign = malign
		}
	}

	return maxAlign
}

func (st *StructType) copyTemplate() DataType {
	newmembers := make(map[string]*StructMember, len(st.Members))

	for k, m := range st.Members {
		newmembers[k] = &StructMember{
			Type:     m.Type.copyTemplate(),
			Constant: m.Constant,
			Volatile: m.Volatile,
		}
	}

	return &StructType{Members: newmembers, Name: st.Name, Packed: st.Packed}
}
