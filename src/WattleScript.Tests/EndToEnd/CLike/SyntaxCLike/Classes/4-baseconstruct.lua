class A
{
    A(value) 
    {
        this.value = value;
    }
}

class B : A
{
    B(value)
    {
        base(value);
        this.value2 = value;
    }
}

var x = new B("hello");
print(x.value2 == x.value);