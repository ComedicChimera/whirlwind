package analysis

import (
	"github.com/ComedicChimera/whirlwind/src/common"
	"github.com/ComedicChimera/whirlwind/src/types"
)

// operatorExists check if an operator is already defined in the given file
// and package (exported so import logic can use it to import operator overloads)
func OperatorExists(pkg *common.WhirlPackage, file *common.WhirlFile, opKind int, signature types.DataType) bool {
	if overloads, ok := pkg.OperatorOverloads[opKind]; ok {
		for _, gop := range overloads {
			if operatorSigsEquivalent(gop.Signature, signature) {
				return true
			}
		}
	}

	if lopsigs, ok := file.LocalOperatorOverloads[opKind]; ok {
		for _, lopsig := range lopsigs {
			if operatorSigsEquivalent(lopsig, signature) {
				return true
			}
		}
	}

	return false
}

// operatorSigsEquivalent checks if two operator signatures are equivalent to a
// degree that defining them both would cause a conflict
func operatorSigsEquivalent(sigA, sigB types.DataType) bool {
	// TODO
	return false
}

// addOperatorOverload adds a global operator overload to the current package
func (w *Walker) addOperatorOverload(opKind int, signature types.DataType) bool {
	if OperatorExists(w.Builder.Pkg, w.File, opKind, signature) {
		return false
	}

	if overloads, ok := w.Builder.Pkg.OperatorOverloads[opKind]; ok {
		w.Builder.Pkg.OperatorOverloads[opKind] = append(overloads, &common.WhirlOperatorOverload{
			Signature: signature,
			Exported:  w.DeclStatus == common.DSExported,
		})
	} else {
		w.Builder.Pkg.OperatorOverloads[opKind] = []*common.WhirlOperatorOverload{
			&common.WhirlOperatorOverload{
				Signature: signature,
				Exported:  w.DeclStatus == common.DSExported,
			},
		}
	}

	return true
}
