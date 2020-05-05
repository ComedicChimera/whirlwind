package types

type FreeType struct {
	id  int
	ftt map[int]DataType
}

func NewFreeType(ftt map[int]DataType) *FreeType {
	id := len(ftt)
	ftt[id] = nil
	return &FreeType{id: id, ftt: ftt}
}

func (f *FreeType) Deduce(ddt DataType) bool {
	if dt := f.ftt[f.id]; dt == nil {
		f.ftt[f.id] = ddt
		return true

	}

	return false
}

func (f *FreeType) coerce(to DataType) bool {
	if v := f.ftt[f.id]; v == nil {
		f.Deduce(to)
		return true
	} else {
		return CoerceTo(to, v)
	}
}

func (f *FreeType) equals(other DataType) bool {
	if v := f.ftt[f.id]; v == nil {
		f.Deduce(other)
		return true
	} else {
		return v.equals(other)
	}
}

func (f *FreeType) cast(to DataType) bool {
	if v := f.ftt[f.id]; v == nil {
		f.Deduce(to)
		return true
	} else {
		return CastTo(to, v)
	}
}
