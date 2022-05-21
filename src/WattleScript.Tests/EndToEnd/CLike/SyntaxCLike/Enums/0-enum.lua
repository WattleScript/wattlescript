function errprint(code) {
    local status, err = pcall(code)
    print(err)
}

enum Colors {
    Red,
    Green,
    Blue,
    Orange = 4,
    Purple,
    Pink = 4,
    Grey,
    Aquamarine = (1 << 4),
}

print(Colors.Red)
print(Colors.Green)
print(Colors.Blue)
print(Colors.Orange)
print(Colors.Purple)
print(Colors.Pink)
print(Colors.Grey)
print(Colors.Aquamarine)

errprint(() => { Colors.Red = 2 })
