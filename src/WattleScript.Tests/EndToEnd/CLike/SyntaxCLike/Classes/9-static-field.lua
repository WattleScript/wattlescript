class A
{
    static MyProp = "test"
    static function F1() {
        return this.MyProp + "2"
    }
}

function F1() {
    return "error"
}

print(A.MyProp)
print(A.F1())