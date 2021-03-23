# Annotations and Metadata

This file lists the names, usages, and effects of annotations and metadata tags.

## Annotations

| Tag | Usage | Arguments | Effect |
| --- | ----- | --------- | ------ |
| `external`| Functions | *none* | Declares that a function is defined in another module |
| `dllimport` | Functions | `dll_name`, `func_name` | Declares that a function is defined in a DLL |
| `intrinsic` | Functions | *none* | Declares that a function is implemented by the compiler |
| `introspect` | Functions, Interfaces | *none* | Allows the function or interface to access internal state fields of a core type (eg. lists) |
| `packed` | Typedefs | *none* | Denotes that a struct definition should be packed instead of padded |
| `vec_unroll` | Functions | *none* | Indicates that the compiler should attempt to unroll any vector loops it finds in a function |
| `no_warn` | Functions | *none* | Prevents warnings from occurring within a function |
| `impl` | Typedefs | `name` | Denotes that a typedef implements a core type |
| `inline` | Functions | *none* | Indicates that the compiler should inline this function if possible |
| `tail_rec` | Functions | *none* | Indicates that the compiler should try to force tail-recursion optimization |
| `hot_call` | Functions | *none* | Indicates that the compiler should optimize for fast-calling on this function (more than normal -- hot function) |
| `no_inline` | Functions | *none* | Indicates that a function should never be inlined |

## Metadata

| Tag | Arguments | Effect |
| --- | --------- | ------ |
| `nocompile` | *none* | Causes the compiler to skip compilation of this file |
| `arch` | `arch_name` | Causes the compiler to only compile this file for the given architecture |
| `os` | `os_name` | Causes the compiler to only compile this file for the given OS |
| `unsafe` | *none* | Marks the file as *unsafe* |
| `no_warn` | *none* | Prevents warnings for the entire file |
| `warn` | `warning_string` | Emits a custom warning to the User |
| `no_util` | *none* | Prevents the default *prelude util* import |