// Testing that an empty __ctor is generated on base classes
// That have ctor

class A { }
class B : A { 
    B() { base() }
    printval() => print("yes");
}

new B().printval();
