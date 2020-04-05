# Type Conversions

| Whirlwind | LLVM | Notes |
| --------- | ---- | ----- |
| `any` | `__any` | |
| `none` | `void` | Shouldn't ever be used as an actual type |
| `null` | _ | Doesn't compile to anything because it can't occur as a type: it compiles to whatever it needs to |
| `[]T` | `__array<T>` | |
| `[T]` | `__list<T>` | |
| `[K: V]` | `__dict<K, V>` | |
| `int` | `i32` | |
| `short` | `i16` | |
| `char` | `i32` | |
| `float` | `f32` | |
| `double` | `f64` | |
| `bool` | `i1` | |
| `byte` | `i8` | |
| `long` | `i64` | |
| `str` | `__string` |  |
| `func(T...)(R)` | `R (T...)*` | |
| `interf:name` | `{ i8*, *vtable, i16, i32 }` | The vtable is generated based on the methods of the interface |
| `struct:name` | `type {} name` | The type before is designate how the struct declaration will generated: it isn't part of the type |
| `*T` | `*T` | |
| `(T...)` | `type { T... } ` | |
| `type:name` | _ | Opaque types are just a name; there is no type declared |
| `type:alias` | `alias` | The alias is the resulting LLVM type |
| `type:enum` | `i16` | All empty enumerated types compile as `i16` |
| `type:enum_val | `type { i16, i8*[*], i32 }` | Val2 may be a double or single pointer depending on if the type class can store multiple values |

## Notes

Any type that compiles to struct on this list is considered to be a **reference type** and will compile to some form of pointer in
the majority of cases.  One exception is that struct members that are reference types are not compiled with a pointer (eliminates unnecessary loads)

All signed and unsigned variants will compile the same because LLVM doesn't have signed types.

## Generic Types

Generic types compile as the type they are followed by what monomorphic variant is being used (eg. `generic.int`)

## Self Types

All self types just compile to pointers to whatever they were a self reference to.