mixin A
{
    private x = 7;
}

class B with A
{
    printx()
    {
        print(this.x);
    }
}

var i = new B();
print(i.x);
i.printx();