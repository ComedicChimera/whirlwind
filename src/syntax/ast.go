package syntax

import "log"

// represent the positional range of an AST node
// in the source text (for error handling)
type TextPosition struct {
	StartLn, StartCol int // starting line, starting 0-indexed column
	EndLn, EndCol     int // ending Line, column trailing token (one over)
}

// represents a piece of the Abstract Syntax Tree (AST)
type ASTNode interface {
	IsLeaf() bool // really just a property getter (for checking)
	Position() *TextPosition
}

// an AST leaf is simply a token (at the end of branch)
type ASTLeaf Token

func (*ASTLeaf) IsLeaf() bool {
	return true
}

// text position of leaf is just the position of the token it contains
func (a *ASTLeaf) Position() *TextPosition {
	return &TextPosition{StartLn: a.Line, StartCol: a.Col, EndLn: a.Line, EndCol: a.Col + len(a.Value)}
}

// an AST branch is a named set of leaves and branches
type ASTBranch struct {
	Name    string
	Content []ASTNode
}

func (*ASTBranch) IsLeaf() bool {
	return false
}

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
