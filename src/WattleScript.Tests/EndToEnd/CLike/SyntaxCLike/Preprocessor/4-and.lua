#define VAR1
#define VAR2

#if VAR1 && VAR2
print("ok")
#else
print ("not ok")
#endif

#if VAR1 && VAR3
print("not ok")
#else
print("ok")
#endif