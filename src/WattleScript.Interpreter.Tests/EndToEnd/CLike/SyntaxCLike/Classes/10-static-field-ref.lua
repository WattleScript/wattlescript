class A
{
    static MyProp = "test"
    static MyProp2 = this.MyProp + "2"
}

print(A.MyProp2)