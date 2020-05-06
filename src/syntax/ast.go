package syntax

import "log"

// TextPosition represents the positional range of an
// AST node in the source text (for error handling)
type TextPosition struct {
	StartLn, StartCol int // starting line, starting 0-indexed column
	EndLn, EndCol     int // ending Line, column trailing token (one over)
}

// ASTNode represents a piece of the Abstract Syntax Tree (AST)
type ASTNode interface {
	Position() *TextPosition
}

// ASTLeaf is simply a token in the AST (at the end of branch)
type ASTLeaf Token

// Position of a leaf is just the position of the token it contains
func (a *ASTLeaf) Position() *TextPosition {
	return &TextPosition{StartLn: a.Line, StartCol: a.Col, EndLn: a.Line, EndCol: a.Col + len(a.Value)}
}

// ASTBranch is a named set of leaves and branches
type ASTBranch struct {
	Name    string
	Content []ASTNode
}

// Position of a branch is the starting position of its first node
// and the ending position of its last node (node can be leaf or branch)
func (a *ASTBranch) Position() *TextPosition {
	// Note: empty AST nodes SHOULD never occur, but we check anyway (so if they do, we see the error)
	if len(a.Content) == 0 {
		log.Fatal("Unable to take position of empty AST node")
		// if there is just one item in the branch, just return the position of that item
	} else if len(a.Content) == 1 {
		return a.Content[0].Position()
		// otherwise, it is the positions that border the leaves (they occur in order so we can take their position as such)
	} else {
		first, last := a.Content[0].Position(), a.Content[len(a.Content)-1].Position()

		return &TextPosition{StartLn: first.StartLn, StartCol: first.StartCol, EndLn: last.EndLn, EndCol: last.EndCol}
	}

	// unreachable
	return nil
}
