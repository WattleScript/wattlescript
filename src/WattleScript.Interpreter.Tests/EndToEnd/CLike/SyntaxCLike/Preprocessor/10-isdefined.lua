#define VAR1

#if defined(VAR1)
print("ok")
#else
print("not ok VAR1")
#endif

#if !defined(VAR2)
print("ok")
#else
print("not ok VAR2")
#endif