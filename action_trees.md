# Raw Action Tree Reference

## Value Nodes

A value node represents a concrete value, does not
connote any operation or computation.

| Name | Type | Value |
| ---- | ---- | ----- |
| Name | string | The name of tree |
| Type | IDataType | The type of the value |
| Value | string | The value of the value node |

### Possible Values

| Name | Purpose | Value |
| ---- | ------- | ----- |
| Literal | Represents a literal value | *Any Literal Value* |
| This | Represents an pointer to any valid instance | *empty* |

## Identifier Node

An identifier node represents a symbol accessed in the tree.

| Name | Type | Value |
| ---- | ---- | ----- |
| Name | string | `"Identifier"`
| Type | IDataType | The symbol's data type |
| IdName | string | The symbol's name |
| Constant | bool | Whether or not the symbol is a constant |

## Tree Node

Represents a multi-value structure or some form of operation.

| Name | Type | Value |
| ---- | ---- | ----- |
| Name | string | The name of the tree |
| Type | IDataType | The return type of the tree |
| Nodes | List\<ITypeNode\> | The list of subnodes / parameters |

### Tree Names

| Name | Parameters | Purpose |
| ---- | ---------- | ------- |
| Array | *Array Elements* | An array |
| List | *List Elements* | A list |
| Map | *Map Pairs* | An array of key value pairs |
| MapPair | Key, Value | A single key value pair |
| Tuple | *Tuple Elements* | A tuple |
| Closure | *Parameters*, Function Body | A closure object |
| TypeCast | Value | A type cast - return type is desired type | 
