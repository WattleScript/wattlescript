mixin B
{
    public y = 9;
}
class A with B
{
    public x = 8;
}

print(new A().x)
print(new A().y)