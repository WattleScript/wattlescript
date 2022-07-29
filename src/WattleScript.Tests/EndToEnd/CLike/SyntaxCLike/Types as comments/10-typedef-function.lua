class Animal {
    Name : string = ""
}

animal : Animal = {
    Name: "my animal",
    Bark: (what : string) => { return `bark {what}` }
}

print(animal.Bark("woof"))
print(animal.Bark("meow"))