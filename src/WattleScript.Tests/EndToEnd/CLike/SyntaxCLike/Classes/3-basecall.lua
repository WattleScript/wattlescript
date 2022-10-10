class A 
{
    printx() => print("x");
}

class B : A
{
    printx2() => base.printx();
}

new B().printx2();
