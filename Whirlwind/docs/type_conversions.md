# Type Conversions

| Whirlwind | LLVM | Notes |
| --------- | ---- | ----- |
| `any` | `i8*` | |
| `none` | `void` | Shouldn't ever be used as an actual type |
| `null` | _ | Doesn't compile to anything because it can't occur as a type: it compiles to whatever it needs to |
| `[]T` | `std.array<T>` | |
| `[T]` | `std.list<T>` | |
| `[K: V]` | `std.dict<K, V>` | |
| `int` | `i32` | |
| `char` | `i16` | |
| `float` | `f32` | |
| `double` | `f64` | |
| `bool` | `i1` | |
| `byte` | `i8` | |
| `long` | `i64` | |
| `str` | std.string | Can be either ASCII or Unicode encoded depending on the context |
| `func(T...)(R)` | `R (T...)*` | |
| `interf:name` | `i8*` | |
| `struct:name` | `type {} name` | The type before is designate how the struct declaration will generated: it isn't part of the type |
| `*T` | `*T` | *See special cases for pointers* |
| `(T...)` | `type { T... } ` | |
| `type:name` | _ | Opaque types are just a name; there is no type declared |
| `type:alias` | `alias` | The alias is the resulting LLVM type |
| `type:enum` | `i32` | All empty enumerated types compile as `i32` |
| `type:enum_val | `type { i32, i8* }` | This is for any type class that stores value in its enumerate variants. `*void` does have copy semantics |

## Notes

All types that contain pointers (except for pointers themselves) will copy both their data and their pointer
when copied, not just point.  They are all passed completely by value.  __The only exception is a function pointer__.

All signed and unsigned variants will compile the same because LLVM doesn't have signed types.

Async and non-async functions compile to the same type.

## Generic Types

Generic types compile as the type they are followed by what monomorphic variant is being used (eg. `generic.int`)

## Self Types

All self types just compile to pointers to whatever they were a self reference to.

## Special Cases for Pointers

| Case | Actual Result |
| ---- | ------------- |
| `*any` | `i8*` |
| `*interf:name` | `i8*` |
| `*type:name` | `i8*` |
| `*type:enum_val` | `type { i32, i8* }*` | |

All `i8*` do NOT have copy semantics applied to them ie. they are treated like normal pointers instead of having their data copied.