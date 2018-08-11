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
| SubExpr | Expression | A sub expression enclosed in parentheses |
| Array | *Array Elements* | An array |
| List | *List Elements* | A list |
| Map | *Map Pairs* | An array of key value pairs |
| MapPair | Key, Value | A single key value pair |
| Tuple | *Tuple Elements* | A tuple |
| Closure | *Parameters*, Function Body | A closure object |
| TypeCast | Value | A type cast - return type is desired type | 
| SizeOf | Value / Type | Size Of operator |
| HeapAlloc | Size | Allocate a set amount of memory on the heap |
| HeapAllocType | Type | Allocate enough memory to store a given type |
| Await | Async Function | Set a function to run on the current thread |
| MapComprehension | Root, Iterator, Expression, \[Filter\] | Perform a map comprehension |
| Comprehension | Root, Iterator, Expression, \[Filter\] | Perform a comprehension |
| Iterator | *Identifiers* | Create an iterator over the some list |
| CreateTemplate | Template | Converts a template to a value, return type is template type |
| GetMember | Root, Identifier | Gets a member of a struct, module or other type |
| InitList | *Initializers* | Create an instance from an initializer list |
| Intializer | Identifier, expr | Initializer a given value in an initializer list |
| FunctionAggregator | Root, AggrFn | Call an aggregator using a function |
| OperatorAggregator | Root, Operator | Call an aggregator using an operator |
| Call | Function, *Arguments* | Call a normal function with given arguments |
| CallAsync | AsyncFunction, *Arguments* | Call an async function with the given arguments |
| CallConstructor | Module, *Arguments* | Call a module constructor |
| Subscript | Collection, Index | Get the element at a given index |
| Slice | Collection, Begin, End | Create a slice between two indices |
| SliceBegin | Collection, Begin | Create a slice from the beginning onward |
| SliceEnd | Collection, End | Create a slice from index 0 to the a given ending |
| SliceStep | Collection, Begin, End, Step | Create a slice between two indices with the given step |
| SliceBeginStep | Collection, Begin, Step | Create a slice from the beginning onward with the given step |
| SliceEndStep | Collection, End, Step | Create a slice from index 0 to the given index with the given step |
| SlicePureStep | Collection, Step | Create a slice with the given step |
| ChangeSign | Numeric | Change the sign of a numeric type |
| Increment | Numeric | Increment a value |
| PostfixIncrement | Numeric | Postfix increment a value |
| Decrement | Numeric | Decrement a value |
| PostfixDecrement | Numeric | Postfix decrement a value |
| Reference | Value | Create a reference to the given value |
| Dereference | Value | Dereference a value a certain number of times equivalent to the **pointer difference** |
| Pow | *Values* | Perform an exponent operation |
| Add | *Values* | Perform an addition/concatination operation |
| Sub | *Values* | Perforn a subtraction operation |
| Mul | *Values* | Perform a multiplication operation |
| Div | *Values* | Perform a division operation |
| Mod | *Values* | Perform a modulo operation |
| LShift | *Values* | Perform a left shift operation |
| RShift | *Values* | Perform a right shift operation |
| Not | Value | Perform a not/bitwise not operation |
| Gt | *Values* | Perform a greater than comparison |
| Lt | *Values* | Perform a less than comparison |
| Eq | *Values* | Perform an equality comparison |
| Neq | *Values* | Perform an unequality comparison |
| GtEq | *Values* | Perform a greater than or equal to comparison |
| LtEq | *Values* | Perform a less than or equal to comparison |
| Or | *Values* | Perform an or/bitwise or logical operation |
| Xor | *Values* | Perform a xor/bitwise xor logical operation |
| And | *Values* | Perform an and/bitwise and logical operation |
| InlineComparison | Comparison Expr, Option1, Option2 | Perform an inline comparison |
| NullCoalesce | NullableExpr, DefaultExpr | Perform a null coalescion operation |