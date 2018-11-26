# Action Tree Reference

## Value Nodes

A concrete value, does not
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
| Operator | Represents an operator value use aggregator | OpName |

## Identifier Node

An identifier node represents a symbol accessed in the tree.

| Name | Type | Value |
| ---- | ---- | ----- |
| Name | string | `"Identifier"` |
| Type | IDataType | The symbol's data type |
| IdName | string | The symbol's name |
| Constant | bool | Whether or not the symbol is a constant |

## Constexpr Node

A constexpr identifier that also stores its value

| Name | Type | Value |
| ---- | ---- | ----- |
| Name | string | `"Constexpr"` |
| Type | IDataType | The symbol's data type |
| IdName | string | The symbol's name |
| ConstValue | string | The constexpr value of the symbol |

## Expr Node

A multi-value structure or some form of operation.

| Name | Type | Value |
| ---- | ---- | ----- |
| Name | string | The name of the expr |
| Type | IDataType | The return type of the expr |
| Nodes | List\<ITypeNode\> | The list of subnodes / parameters |

### Expr Names

| Name | Parameters | Purpose |
| ---- | ---------- | ------- |
| Array | *Array Elements* | An array |
| List | *List Elements* | A list |
| Dictionary | *KV Pairs* | An array of key value pairs |
| KVPair | Key, Value | A single key value pair |
| Tuple | *Tuple Elements* | A tuple |
| Closure | *Parameters*, Function Body | A closure object |
| TypeCast | Value | A type cast - return type is desired type | 
| HeapAlloc | Size | Allocate a set amount of memory on the heap |
| HeapAllocType | Type | Allocate enough memory to store a given type |
| Await | Async Function | Set a function to run on the current thread |
| DictComprehension | Iterator, Expression, Expression, \[Filter\] | Perform a map comprehension |
| Comprehension | Iterator, Expression, \[Filter\] | Perform a comprehension |
| Filter | expr | A filter component for a comprehension |
| Iterator | Root, *Identifiers* | Create an iterator over the some iterable |
| CreateTemplate | Template | Converts a template to a value, return type is template type |
| GetMember | Root, Identifier | Gets a member of a struct, object or other type |
| InitList | *Initializers* | Create an instance from an initializer list |
| Intializer | Identifier, expr | Initializer a given value in an initializer list |
| Call | Function, *Arguments* | Call a normal function with given arguments |
| CallAsync | AsyncFunction, *Arguments* | Call an async function with the given arguments |
| CallConstructor | obj, *Arguments* | Call an object constructor |
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
| Complement | Value | Perform a bitwise complement operation |
| Floordiv | *Values* | Perform a floor division operation |
| InlineComparison | Comparison Expr, Option1, Option2 | Perform an inline comparison |
| NullCoalesce | NullableExpr, DefaultExpr | Perform a null coalescion operation |

### Statement Components

ExprNodes made specifically for use in a certain statements as structural components.

| Name | Parameters | Purpose |
| ---- | ---------- | ------- |
| EnumConstInitializer | Identifier, Expr | Initialize an enumerated constant with a custom start |
| AssignVars | *Assignment Variables* | The list of variables being assigned to |
| AssignExprs | *Exprs* | The list of corresponding variable assignment expressions |
| ConstexprInitializer | Expr | A constexpr initializer for constexpr declarations |
| Initializer | Expr | An initializer for variable / constant declarations |
| Var | Identifier, \[*Initializer*\] | A single variable in variable declaration unit |
| Variables | *Vars* | A list of all the individual variables in a single variable declaration |

### Block Statement Components

ExprNodes made specifically for use in certain block statements (`if`, `else`, ect.) as structural components.

| Name | Parameters | Purpose |
| ---- | ---------- | ------- |
| CForExpr | \[IterVarDecl\] \[CForCondition\] \[CForUpdateExpr \| CForUpdateAssignment \] | Contains the contents of the C-style for loop |
| CForCondition | Expr | The end condition of a C-style for loop |
| CForUpdateExpr | Expr | The expression to be called each cycle of a C-style for loop |
| CForUpdateAssignment | Assignment | The assignment statement to evaluated after each cycle of a C-Style for loop |

## Statement Node

Represents a single Whirlwind statement.

| Name | Type | Value |
| ---- | ---- | ----- |
| Name | string | The name of the statement |
| Type | IDataType | `void` |
| Nodes | List\<ITypeNode\> | The nodes that make up the statement args |

### Statement Values

| Name | Parameters | Purpose | Error Bubble |
| ---- | ---------- | ------- | ----------- |
| Break | `none` | Break out of a breakable loop or region | **STATEMENT** |
| Continue | `none` | Continue onto the next cycle in a loop | **STATEMENT** |
| DeclareVariable | *Variable Nodes* | Declare a variable or set of variables | **BLOCK** |
| DeclareConstant | *Constant Nodes* | Declare a constant or set of constants | **BLOCK** |
| DeclareConstexpr | *Constexpr Nodes* | Declare a constexpr or set of constexprs | **BLOCK** |
| ThrowException | Exception | Throw a normal exception | **BLOCK** |
| ThrowObject | Object | Wrap an object in an exception and throw it | **BLOCK** |
| Delete | *Identifiers* | Delete a set of symbols | **BLOCK** (**STATEMENT** in bytecode stage) |
| Return | Value | Return a value from a function | **BLOCK** |
| ExprStmt | Expr | Represents an expression being asserted to a statement | **STATEMENT** |
| EnumConst | \[*EnumConstInitializer*\], *Identifiers* | Declare an enumerated set of constants | **BLOCK**|
| EnumConstExpr | \[*EnumConstInitializer*\], *Identifiers* | Declare an enum const with a custom constexpr initializer | **BLOCK** |
| Assignment | Variables, Initializers | Assign a set of variables to a new value | **STATEMENT** |

### The `None` Statement
The none statement is a statement that contains no children and is called `None`.  This statement is used as a placeholder
during the visitation stage.  It is used to prevent errors in action tree construction and is never actually compiled as
any program that contains none statements also contains errors.

## Block Node

Represents a single Whirlwind block statement or declaration.

| Name | Type | Value |
| ---- | ---- | ----- |
| Name | string | The name of the block |
| Type | IDataType | `void` |
| Nodes | List\<ITypeNode\> | The nodes that make the defining factors of the block |
| Block | List\<ITypeNode\> | All of the sub-nodes contained within each items block |

### Block Values

| Name | Parameters | Purpose |
| ---- | ---------- | ------- |
| If | Expr | A single, unchained if statement |
| CompoundIf | `none` | A compound if chain containing at least two statements where each statement is stored in its block |
| Elif | Expr | A chained elif statement |
| Else | `none` | A chained else statement |
| Select | Expr | The select statement whose block contains each case and/or the default |
| Case | *Expr* | A case block of a select statement |
| Default | `none` | The default block of a select statement |
| ForInfinite | `none` | An infinite loop |
| ForIter | Iterator | A foreach / for-iterator loop |
| ForCondition | Expr | A conditional for loop (while loop) |
| CFor | CForExpr | A C-style for loop |
| Except | *Handle* | A block that contains a series of handles |
| Handle | Expression, Identifier | A single handle expression |
| FromStmt | Expr, Identifier, [FromExcept] | A from borrow statement |
| FromExcept | `none` | A fail case for a from borrow statement |
| FromBlock | VarDecl | A context manager |
