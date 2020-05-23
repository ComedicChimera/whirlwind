package types

// StructType represents a named explicitly structured data type
type StructType struct {
	Name    string
	Members map[string]StructMember
	Packed  bool
}

// StructMember represents a member of a structured type.  It includes
// certain salient value properties of the member for utilities sake
// and is stored as a pointer because it should ideally only be created
// once per data structure and needs to be comparable by value
type StructMember struct {
	Type     DataType
	Constant bool
	Volatile bool
}

// NewStructType creates a new named, structured data type in given package with the
// given members.  Requires a flag to indicate whether or not the struct is packed
func NewStructType(srcPkg string, name string, members map[string]StructMember, packed bool) DataType {
	return newType(&StructType{Name: srcPkg + "::" + name, Members: members, Packed: packed})
}

func (st *StructType) coerce(dt DataType) bool {
	return false
}

func (st *StructType) equals(dt DataType) bool {
	return false
}

func (st *StructType) cast(dt DataType) bool {
	return false
}

// Repr of a named type is simply its name
func (st *StructType) Repr() string {
	return st.Name
}

// SizeOf a structure depends on whether or not it
// is packed.  If it is packed then it is the sum
// of the individual element sizes.  Otherwise, it
// is the padded size of the data structure (for alignment)
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

// AlignOf a struct depends on whether or not
// it is packed. If it is padded, then the
// alignment is the largest alignment of its
// members.  Otherwise, it is (TODO)
// NOTE: On the backend, structs are more
// often implemented as pointers which means
// that within data structures, their alignment
// should be that of a pointer (!IMPORTANT)
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
