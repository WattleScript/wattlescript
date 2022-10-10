class A 
{
}

class B : A
{
    dothing() => {
        base = 7; //Cannot write to base, compiler error.
        print("hello");
    }
}

new B().dothing();