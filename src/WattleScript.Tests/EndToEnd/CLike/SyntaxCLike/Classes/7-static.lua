class A
{
    function f3() {
        return 8
    }

    static function f() {
        return 10
    }
    
    static function f2() {
        return 12
    }
}

function g() {
    return 6
}

print(A.f());
print(A.f2());
print(A.f());
print(new A().f3());
print(g())
print(A.f());
print(A.f2());