#define STRING = "hello", EMPTY, STRING2 = "world"

print(STRING)
print(STRING2)
print(EMPTY)
#undef STRING, STRING2

//we didn't undef empty, check it's still there
#if EMPTY
print("ok")
#else
print("not ok - EMPTY")
#endif

#if !STRING
print("ok")
#else
print("not ok - STRING")
#endif

#if !STRING2
print("ok")
#else
print("not ok - STRING2")
#endif