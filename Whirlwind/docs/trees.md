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
| Null | Represents a use of the null keyword | *empty* |
| Type | Represents the data type used in is type testing | *empty* |
| Value | Represents the chained data value | *empty* |
| Super | Represents a reference to the single parent of a type interface | "()" |
| ByteLiteral | Represents a byte literal value | *Any byte literal* |

### Components

| Name | Purpose |
| ---- | ------- |
| Type | Store a data type |
| StandardImplement | Store a standard inherit in an interface bind |
| GenericImplement | Store a generic inherit in a generic interface bind |
| GenericTypeInterface | Store the type of a generic type interface |
| GenerateThis | Used in generic interface binding to store the `this` value of any given generate |
| BindType | Stores the type being bound to in an interface bind |
| TypeInterface | Store the type of a regular type interface |
| Package | Store the type of a package during inclusion |
| ConstructorSignature | Store the type of a constructor in a constructor block |
| VariantGenerate | Represents the type generated for a specific variant instance |
| AnnotationName | Stores the name of any given annotation |
| AnnotationValue | Stores the value being given to an annotation | *Any string value* |
| IntegerMember | Stores the "name" of the member being accessed from a tuple |
| Rename | Stores the new name of a package in each file |
| Operator | Stores the operator being overloaded in an op. overload |

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
| Lambda | *Parameters*, Function Body | A lambda object |
| PartialFunction | Expr, *Removed Arguments* | Create a partially-completed function from the expression |
| PartialArg# | Expr | Represents a partial argument where "#" is replaced with the number of the argument |
| HeapAllocSize | Size | Allocate a set amount of memory on the heap |
| HeapAllocType | Type | Allocate enough memory to store a given type |
| HeapAllocStruct | Struct, *Arguments* | Create a new heap allocated struct and call its constructor where needed |
| Await | Async Function | Set a function to run on the current thread |
| DictComprehension | Iterator, Expression, Expression, \[Filter\] | Perform a dictionary comprehension |
| ListComprehension | Iterator, Expression, \[Filter\] | Perform a list comprehension |
| ArrayComprehension | Iterator, Expression, \[Filter\] | Perform an array comprehension |
| Filter | expr | A filter component for a comprehension |
| Iterator | Root, *Identifiers* | Create an iterator over the some iterable |
| CreateGeneric | Generic | Converts a generic to a value, return type is generic type |
| GetMember | Root, Identifier | Gets a member of a struct or other type |
| GetTIMethod | Root, Identifier | Get a method of an item's type interface |
| StaticGet | Root, Identifier | Gets a static member of the given type |
| GetTupleMember | Root, IntegerMember | Gets an element from a tuple |
| DerefGetMember | Root, Identifier | Performs a dereference and gets a member |
| DerefGetTIMethod | Root, Identifier | Performs a dereference and gets a type interface method |
| NullableDerefGetMember | Root, Identifier | Performs a nullable dereference and a nullable get member |
| NullableDerefGetTIMethod | Root, Identifier | Performs a nullable dereference and gets a type interface method |
| InitList | *Initializers* | Create an instance from an initializer list |
| Initializer | Identifier, expr | Initializer a given value in an initializer list |
| Call | Function, *Arguments* | Call a normal function with given arguments |
| CallAsync | AsyncFunction, *Arguments* | Call an async function with the given arguments |
| CallConstructor | Struct, *Arguments* | Call a struct constructor |
| CallFunctionOverload | FunctionGroup, *Arguments* | Call an overloaded function |
| CallGenericOverload | GenericGroup, *Arguments* | Call a generic overloaded function |
| CallTCConstructor | NewType, *Arguments* | Creates a new type for an algebraic data type |
| Subscript | Collection, Index | Get the element at a given index |
| Slice | Collection, Begin, End | Create a slice between two indices |
| SliceBegin | Collection, Begin | Create a slice from the beginning onward |
| SliceEnd | Collection, End | Create a slice from index 0 to the a given ending |
| SliceStep | Collection, Begin, End, Step | Create a slice between two indices with the given step |
| SliceBeginStep | Collection, Begin, Step | Create a slice from the beginning onward with the given step |
| SliceEndStep | Collection, End, Step | Create a slice from index 0 to the given index with the given step |
| SlicePureStep | Collection, Step | Create a slice with the given step |
| ChangeSign | Numeric | Change the sign of a numeric type |
| Complement | Value | Perform a bitwise complement operation |
| Increment | Numeric | Increment a value |
| PostfixIncrement | Numeric | Postfix increment a value |
| Decrement | Numeric | Decrement a value |
| PostfixDecrement | Numeric | Postfix decrement a value |
| Indirect | Value | Get a pointer to the given value |
| Dereference | Value | Dereference a pointer a certain number of times equivalent to the **pointer difference** |
| Pow | *Values* | Perform an exponent operation |
| Add | *Values* | Perform an addition/concatination operation |
| Sub | *Values* | Perforn a subtraction operation |
| Mul | *Values* | Perform a multiplication operation |
| Div | *Values* | Perform a division operation |
| Mod | *Values* | Perform a modulo operation |
| Floordiv | *Values* | Perform a floor division operation |
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
| Bind | Value, *Functions* | Binds each expression to the one on its right |
| Compose | *Functions* | Composes the functions sequentially |
| InlineComparison |  Option1, ComparisonExpr, Option2 | Perform an inline comparison |
| Is | Expr, Type | Tests if an object is a given data type |
| ExtractInto | Expr, Identifier | Extracts the value from the given expression if possible and stores it in a expr-local |
| Range | Expr, Expr | Creates a range between the two expressions |
| TypeCast | Expr, Type | Converts a type class value into a more specific form |
| SelectExpr | *Cases*, \[Default\] | An inline select expression |
| Then | Expr, Expr | Chains one expr into another |
| Super | Expr | Gets the parent based on the given name |
| From | Expr | Extract a value from an algebraic type |
| ExprVarDecl | Identifier, Expr | Declare an expr-local variable |
| NamedArgument | Identifier, Expr | Names an argument in a function declaration |
| ThisDereference | Atom | Connotes an implicit dereference of the `this` pointer |

*All of the subscript and slice trees have an option to have `SetOverload` on the end of them where appropriate*

### Statement Components

ExprNodes made specifically for use in a certain statements as structural components.

| Name | Parameters | Purpose |
| ---- | ---------- | ------- |
| AssignVars | *Assignment Variables* | The list of variables being assigned to |
| AssignExprs | *Exprs* | The list of corresponding variable assignment expressions |
| ConstexprInitializer | Expr | A constexpr initializer for constexpr declarations |
| Initializer | Expr | An initializer for variable / constant declarations |
| Var | Identifier, \[*Initializer*\] | A single variable in variable declaration unit |
| Variables | *Vars* | A list of all the individual variables in a single variable declaration |
| IncludeSet | *Vars* | Represents a list of the variables included in an include statement |

### Block Components

ExprNodes made specifically for use in certain block statements and/or declarations (`for`, `type`, etc.) as structural components.

| Name | Parameters | Purpose |
| ---- | ---------- | ------- |
| CForExpr | \[IterVarDecl\] \[CForCondition\] \[CForUpdateExpr \| CForUpdateAssignment \] | Contains the contents of the C-style for loop |
| IterVarDecl | Identifier | Declares an iterator variable for a C-style for loop |
| CForCondition | Expr | The end condition of a C-style for loop |
| CForUpdateExpr | Expr | The expression to be called each cycle of a C-style for loop |
| CForUpdateAssignment | Assignment | The assignment statement to evaluated after each cycle of a C-Style for loop |
| NewType | *Values*, \[ValueRestrictor\] | The new type value in a type class |
| AliasType | \[Identifier\], \[ValueRestrictor\] | Indicates an alias value in a type class |
| BindInterface | Interface, Type | Binds an interface to the given data type |
| Implements | *Types* | A list of interfaces implemented by a given type interface |
| MemberInitializer | Identifier, Expr | The initializer for any struct member that has one |
| DecoratorCall | Expr | Used in processing a decorator expression; represents a call to the decorator |
| ValueRestrictor | Expr | Restricts the value of any type class enumerated value |
| Case | Represents a case in a case expression |
| Default | Represents the default branch in a case expression |

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
| Delete | *Identifiers* | Delete a set of symbols | **BLOCK** |
| Return | *Value* | Return a value from a function | **BLOCK** |
| Yield | *Value* | Yield-return a value from a function | **BLOCK** |
| ExprStmt | Expr | Represents an expression acting as a statement | **STATEMENT** |
| ExprReturn | Expr | Represents an implicit return in an expression function | **BLOCK** |
| Assignment | AssignVars, AssignExprs | Assign a set of variables to a new value | **STATEMENT** |
| Annotation | AnnotationName, \[ AnnotationValue \] | A compiler annotation | **BLOCK** |
| Include | Package, \[ Rename \] | Include a package | **BLOCK** |
| IncludeSet | IncludeSet, Package | Include a set of values from a package | **BLOCK** |
| IncludeAll | Package | Include all of the values of a package into the current namespace | **BLOCK** |

*All of the include statements can be prefixed by "Export" to designate that they export everything they include.*

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

### Block Values (Block Statements)

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
| ContextManager | VarDecl | A context manager |
| LambdaBody | `none` | A body within a lambda expression |
| Subscope | `none` | A new subscope |
| CaptureBlock | `none` | A capturing block; capture is stored in symbol table |
| After | `none` | A block that runs after certain blocks under certain conditions |

### Block Values (Block Declarations)

All block declaration containing identifiers are using the identifier to represent their signature.

| Name | Parameters | Purpose |
| ---- | ---------- | ------- |
| Function | Identifier | A function declaration |
| AsyncFunction | Identifier | An async declaration |
| Struct | Identifier | A struct declaration |
| Interface | Identifier | An interface declaration |
| BindInterface | TypeInterface, Type, [Implements] | An interface bind |
| BindGenericInterface | GenericTypeInterface, Type, [Implements] | A generic interface bind |
| Decorator | *Expr* | A function declared with one or more decorators |
| TypeClass | Identifier, *Members* | A type class declaration |
| Constructor | Identifier | A constructor within a struct |
| Generic | Identifier | A generic declaration |
| Variant | VariantGenerate, Identifier | A variant declaration |
| OperatorOverload | Operator | An operator overload declaration |
| AnnotatedBlock | `none` | Declares an annotation wrapping another block |
| Package | `none` | Block enclosing an entire file |
