class A {
    Y = "hello"
    X = this.Y
    Z = 10
    X2 = this.X + this.Z
}

a = new A()
print(a.X)
print(a.X2)