class A
{
    private x = 7;
}

class B : A
{
    printx()
    {
        print(this.x);
    }
}

var i = new B();
print(i.x);
i.printx();