local z = 2

f = (x = () => {print(z)}) => {
    x()
}

z = 3

f();

function test2() {
    local z = 2
    f = (x = () => {print(z)}) => {
        x()
    }
    z = 7;
    return f;
}

local f2 = test2();
f2();