static class X {
    static function Y() { print(10) }
    static T = this.Y
    static function Z() { print(this.T()) }
}

X.Z()