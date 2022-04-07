f = (x = () => {print("yes")}) => {
    x()
}

f(() => {
    print("no")
});