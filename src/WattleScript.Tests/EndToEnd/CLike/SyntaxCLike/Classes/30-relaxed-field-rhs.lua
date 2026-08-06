class C {
    private a, b, c
    p2
    
    C() {
        this.a = 2
        this.p2 = 20
    }
    
    f2() {
        print(this.a)
    }
}

c = new C()
print(c.a)
c.f2()
print(c.p2)