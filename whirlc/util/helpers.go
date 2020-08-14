package util

// BoolToInt converts a boolean to an int
// @golang would a cast have been so hard?
func BoolToInt(b bool) int {
	if b {
		return 1
	}

	return 0
}

// ThrowError logs an error with the log module
func ThrowError(message, kind string, position *TextPosition) {
	LogMod.LogError(NewWhirlError(
		message, kind, position,
	))
}
