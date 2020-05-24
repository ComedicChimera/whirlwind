package syntax

import "github.com/ComedicChimera/whirlwind/src/util"

// ASTNode represents a piece of the Abstract Syntax Tree (AST)
type ASTNode interface {
	Position() *util.TextPosition
}

// ASTLeaf is simply a token in the AST (at the end of branch)
type ASTLeaf Token

// Position of a leaf is just the position of the token it contains
func (a *ASTLeaf) Position() *util.TextPosition {
	return TextPositionOfToken((*Token)(a))
}

// TextPositionOfToken takes in a token and returns its text position
func TextPositionOfToken(tok *Token) *util.TextPosition {
	return &util.TextPosition{StartLn: tok.Line, StartCol: tok.Col, EndLn: tok.Line, EndCol: tok.Col + len(tok.Value)}
}

// ASTBranch is a named set of leaves and branches
type ASTBranch struct {
	Name    string
	Content []ASTNode
}

// Position of a branch is the starting position of its first node and the
// ending position of its last node (node can be leaf or branch)
func (a *ASTBranch) Position() *util.TextPosition {
	// Note: empty AST nodes SHOULD never occur, but we check anyway (so if they
	// do, we see the error)
	if len(a.Content) == 0 {
		util.LogMod.LogFatal("Unable to take position of empty AST node")
		// if there is just one item in the branch, just return the position of
		// that item
	} else if len(a.Content) == 1 {
		return a.Content[0].Position()
		// otherwise, it is the positions that border the leaves (they occur in
		// order so we can take their position as such)
	} else {
		first, last := a.Content[0].Position(), a.Content[len(a.Content)-1].Position()

		return &util.TextPosition{StartLn: first.StartLn, StartCol: first.StartCol, EndLn: last.EndLn, EndCol: last.EndCol}
	}

	// unreachable
	return nil
}
