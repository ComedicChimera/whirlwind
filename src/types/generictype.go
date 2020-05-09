package types

type GenericType struct {
	Generates []*GenerateType
	Name      string
}

type GenerateType struct {
	Parent     *GenericType
	TypeParams []DataType
}
