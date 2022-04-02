a ??= 10
a ??= 20

print(a)

a = nil

a ??= 50

print(a)

b ??= [5, 15, 30]

print(b[0])

b[0] ??= 50

print(b[0])

b["test"] ??= [7, 14]

print(b["test"][0])