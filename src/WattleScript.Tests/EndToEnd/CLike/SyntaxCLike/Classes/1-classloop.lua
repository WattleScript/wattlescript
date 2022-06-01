function errprint(code) {
    local status, err = pcall(code)
    print(err)
}

class A : B { }
class B : A { printthing() => print("hello"); }

errprint(() => { new B().printthing(); });