class C {
  C() {
    print("ctor side effect")
  }
  
  f() {
    print("something")
  }
}

new C()
new C().f()