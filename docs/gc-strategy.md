# Conditional Garbage Collection

This file documents Whirlwind's garbage collection and memory management strategy.

## High Level Explanation

  - The compiler decides whether a reference needs to be managed, unmanaged, or stack-allocated
  - Many references (even those on the heap) can be managed statically using "simple" lifetime analysis
  - Base calculations on lexical scope instead of function scope (to make sure references have the intended behavior
    -- recall our C example with initializing a linked list)
