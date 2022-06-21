typedef Animal {
    Name : string,
    Bark : function (what : string) : string,
    Callback : function (callback : function (param : object)) : void
}

animal : Animal = {
    Name: "my animal",
    Bark: (what) => { return `bark {what}` }
}

print(animal.Bark("woof"))
print(animal.Bark("meow"))