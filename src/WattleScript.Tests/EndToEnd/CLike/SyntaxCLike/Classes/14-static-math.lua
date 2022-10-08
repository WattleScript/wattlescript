static class Math {
  static PI = 3.1415926535

  static function Sum(a, b) {
    return a + b
  }

  static CircleArea = (r) => {
   return this.PI * r ** 2
  } 
}

print(Math.Sum(5, 1))
print(Math.CircleArea(10))