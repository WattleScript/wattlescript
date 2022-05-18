//#define inside a string literal won't be processed
//but a #define in the expression will

l = `
#define HELLO
{
#define WORLD
}
`
print(CurrentLine())
#if HELLO
print("hello")
#endif
#if WORLD
print("world")
#endif