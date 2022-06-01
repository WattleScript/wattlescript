class A
{
    A(value)
    {
        this.value = value;
    }
    
    printvalue() => print(this.value);
}

new A("hello").printvalue();