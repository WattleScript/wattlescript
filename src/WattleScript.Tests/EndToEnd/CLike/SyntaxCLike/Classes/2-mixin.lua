mixin MixA {
    printvalue() => print(this.value);
}

class A with MixA {
    A(value) {
        this.value = value;
    }
}

new A("hello").printvalue();

mixin MixB {
    printvalue() => print("not ok hello");
}

class B with MixB {
    B(value) {
        this.value = value;
    }
    printvalue() => print(this.value);
}

new B("hello").printvalue(); //class function defs override mixin defs

mixin MixinValue {
    value = "hello!! :)"
}

class C with MixinValue, MixA { } //mixins also add data. multiple mixin usage

new C().printvalue();

//mixins cannot define constructors
mixin M { 
    M2(value) { this.value = value }
}

class M2 with M { }

print(new M2("hello").value == nil);