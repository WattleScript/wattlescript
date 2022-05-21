enum Flags {
    A = (1 << 1),
    B = (1 << 2),
    AB = A | B
}

print(Flags.A);
print(Flags.B);
print(Flags.AB);
