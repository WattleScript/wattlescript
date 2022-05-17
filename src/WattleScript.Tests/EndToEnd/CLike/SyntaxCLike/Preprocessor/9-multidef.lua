#define STRING = "hello", STRING2 = "world"

print(STRING)
print(STRING2)

#undef STRING, STRING2

#if !STRING
print("ok")
#else
print("not ok")
#endif

#if !STRING2
print("ok")
#else
print("not ok")
#endif