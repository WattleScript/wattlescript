class A
{
    static bar = "bar"

    static function f1() {
        return 2
    }
    
    static function f2() {
        return this.f1() + 3 + this.bar
    }
}

print(A.f2())