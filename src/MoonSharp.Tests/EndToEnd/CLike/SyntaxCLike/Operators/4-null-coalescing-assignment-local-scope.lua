f = () => {
    local a ??= 600
    a ??= 700
    print(a)
}

f();
print(a)