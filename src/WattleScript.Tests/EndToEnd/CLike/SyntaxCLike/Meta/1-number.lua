print((1.0).tostring())
print((2.0).tostring())

function prototype.number.double()
{
    return this * 2;
}

print((2.0).double());