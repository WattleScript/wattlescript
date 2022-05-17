#define A 5
#define B 3

#if A > B
print("ok")
#else
print("not ok A > B")
#endif

#if A >= B
print("ok")
#else
print("not ok A >= B")
#endif

#if B > A
print("not ok B > A")
#else
print("ok")
#endif

#if B >= A
print("not ok B >= A")
#else
print("ok")
#endif

#if A < B
print("not ok A < B")
#else
print("ok")
#endif

#if A <= B
print("not ok A <= B")
#else
print("ok")
#endif

#if B < A
print("ok")
#else
print("not ok B < A")
#endif

#if B <= A
print("ok")
#else
print("not ok B <= A")
#endif
print(CurrentLine())

#if 23 > A 
print("ok")
#else
print("not ok 23 > A");
#endif