package resolve

import (
	"github.com/ComedicChimera/whirlwind/src/common"
	"github.com/ComedicChimera/whirlwind/src/syntax"
)

// Definition represents a definition that is still being resolved
type Definition struct {
	// Branch is the ASTBranch associated with the given definition
	Branch *syntax.ASTBranch

	// Unknowns is a list of the unknown symbols needed to produce the HIRNode
	// for the given definition along with their first position in the Branch.
	Unknowns map[string]*common.UnknownSymbol

	// SrcFile is the file this definition occurs in.
	SrcFile *common.WhirlFile
}

// DefinitionQueue is a simple queue implementation used for the definition
// resolution algorithm.  It stores exclusively `Definition` types.
type DefinitionQueue struct {
	// start is the root node for the definition queue (implemented as LinkedList)
	start *DefQueueNode

	// end is the end node/last node in the definition queue
	end *DefQueueNode

	// length is the length of the queue
	len int
}

// DefQueueNode is standard linked list node for the DefinitionQueue
type DefQueueNode struct {
	Value *Definition
	Next  *DefQueueNode
}

// Enqueue adds a new definition to the end of the queue
func (dq *DefinitionQueue) Enqueue(def *Definition) {
	defNode := &DefQueueNode{Value: def}

	if dq.len == 0 {
		dq.start = defNode
	} else {
		dq.end.Next = defNode
	}

	dq.end = defNode
	dq.len++
}

// Dequeue removes the front definition from the queue
func (dq *DefinitionQueue) Dequeue() {
	// the C programmer in me is screaming looking at this :)
	dq.start = dq.start.Next

	dq.len--
}

// Peek reveals the element at the front of the queue
func (dq *DefinitionQueue) Peek() *Definition {
	return dq.start.Value
}

// Rotate rotates the elements in the queue such that the element at the front
// is moved to the back and the next element becomes the front
func (dq *DefinitionQueue) Rotate() {
	// Rotation "Algorithm"
	// --------------------
	// | [0] [1] ... [n] |
	//    ^           ^
	//  start        end
	// Then,
	// | [0] [1] ... [n]>[0] |
	//    ^           --> ^
	//  start            end
	// Finally,
	// | [1] <-- ... [n] [0] |
	//    ^               ^
	//  start            end

	// dq.end is still in the list b/c the previous node points to it
	dq.end.Next = dq.start
	dq.end = dq.start
	dq.start = dq.start.Next
}

// Len returns the length of the queue
func (dq *DefinitionQueue) Len() int {
	return dq.len
}
