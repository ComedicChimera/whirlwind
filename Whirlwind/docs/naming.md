# Naming Conventions

This file outlines the conventions used when translating higher-level constructs
down to LLVM IR.  Each type of renaming gets its own section.

- [Packages](#packages)
- [Group Overloading](#groups)
- [Methods](#methods)
- [Operator Overloading](#operators)
- [Generics & Variants](#generics)

It is also important to note that whenever a type is used in a name, its true toString()
value will be used with no modification.  Whenever either the type or some other part
of the generated name does not comply with LLVM's identifier rules, **the identifier
will simply be encased in quotation marks by LLVMSharp,**, meaning all manner of naming
styles are possible to help make the resultant code as unambiguous as possible.

## <a name="packages"> Packages

Every exported member of a package is given a random 16 character alphanumeric prefix when
it is compiled to prevent naming clashes in the global namespace between translation units.
However, in Whirlwind's global symbol table, no such change occurs; rather, for each package,
any symbols included as part of a package are given that package's **named** prefix.

*Eg.* for the exported symbol `init`, in LLVM's symbol table it might appear as `10ac6_aD78b7bcfA54.init`,
and in Whirlwind's symbol table, it would appear as `pkg::init` (assuming the package's name is `pkg`).

## <a name="groups"> Group Overloading

Group overloading is handled by putting the types of the arguments
subsequent to the definition separated by a `.` as with packages.

*Eg.* for functions `f()` and `f(x: int)`, the resultant names
will be `f` and `f.int`.

If the function has multiple arguments, each argument will be separated
by a comma.

*Eg.* for functions `f(x, y: int)` and `f(s: str, z: float)`, the resultant
names will be `f.int,int` and `f.str,float`.

## <a name="methods"> Methods

Method are compiles as functions that accept a this pointer as their first argument.
Given that, we need to be able to group them with their appropriate type.  The method
for doing this is simple.  We take the name of the original type followed by a `.`
and the keyword `interf` followed by another `.` and the name of the method (plus all
group, operator, or generic suffixes and prefixes appended with said name).

*Eg.* for an interface named `IExample` with two methods `f` and `g`, their names would compile
as `IExample.interf.f` and `IExample.interf.g`

*Eg.* for the same interface, the functions `h(x: int)` and `h(s: str)` exist.  Their names
would compiles as `IExample.interf.h.int` and `IExample.interf.h.str`.

This convention is equivalently applied for type interfaces as well.

*Eg.* for a type interface for `[]int` with method `m`, the name of `m` would compile as
`array[int].interf.m`.

## <a name="operators"> Operator Overloading

Operator overloads compile very similarly to methods.  The main difference is that the method keyword
is replaced with `operator` and the special operator function name is used instead of the normal method
name.

*Eg.* for an interface named `IExample` with an add overload and multiply overload, the corresponding
operator overloads would appear like: `IExample.operator.__+__` and `IExample.operator.__*__`

Notably, each operator overload function is named for its corresponding operator node, not its expression
node since semantically a `+` overload does not need to be an "add" overload in the traditional sense.
This transformation happens in the operator overload declaration at the generation level.

Additionally, the same convention is used with type interfaces.

## <a name="generics"> Generics & Variants

Generics and variants both compile using similar logic.  The only difference is that when a variant is used,
said variants body is substituted in place of the compiler generated generic body for that type.  When the generic
(or variant) is compiled, all of its recorded forms are given their own separate declaration using `.variant` to
signal that it is a generic variant followed by `.` with the various type values being used separated by `,`.

*Eg.* a generic named `Generic` with three detected forms (`int`, `[]float`, and `[str: double]`) and one type label (`T`)
would have three  different names resembling the following: `Generic.variant.int`, `Generic.variant.[]float`, and `Generic.
variant.[str: double]`.
