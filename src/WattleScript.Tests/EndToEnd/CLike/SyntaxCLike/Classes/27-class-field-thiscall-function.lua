class A {
    Y = () => { print("hello") }
    X = this.Y
}

a = new A()
a.X()