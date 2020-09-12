package build

import (
	"github.com/ComedicChimera/whirlwind/src/common"
)

// attachPrelude adds in all additional prelude imports (determined based on a
// file's host package). This function does not proceed beyond stage 1 of
// importing for the prelude (all prelude imports can be treated as normal
// package imports beyond this point).  All errors that occur in this function
// are considered fatal (as they have no direct text position).
func (c *Compiler) attachPrelude(pkg *common.WhirlPackage, file *common.WhirlFile) {

}
