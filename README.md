# only-garbage(*n*)

*n* = number of objects

Only allocates garbage. Should scale linearly with the number of objects.

# percent(*p*, *n*)

*p* = garbage percentage (0-100)

*n* = number of objects

Does a GC when *p* % of the heap is garbage. Should scale linearly with the
number of live objects.
