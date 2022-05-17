#define STRING "hello"
#define NUMBER 8.0
#define BOOLEAN true
#define NONE

// Making sure "//" and '/*' within literals doesn't trigger comments

#define STRING2 "//"
#define STRING3 '/*'

print(STRING)
print(STRING2)
print(STRING3)
print(NUMBER)
print(BOOLEAN)
print(NONE)