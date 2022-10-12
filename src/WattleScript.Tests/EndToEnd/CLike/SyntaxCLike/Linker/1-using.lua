using ws.tests.classTest

namespace ws.tests.entry {
    print("hello world")
    
    class ClassA : ClassB { ClassA() { base() } }
    class ClassC { ClassC() { print("hello world2") } }
    
    a = new ClassA()
}