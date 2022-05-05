myRange = 1..5
print(myRange.from)
print(myRange.to)

myLeftExclusiveRange = 1..<5
print(myLeftExclusiveRange.from)
print(myLeftExclusiveRange.to)

myRightExclusiveRange = 1>..5
print(myRightExclusiveRange.from)
print(myRightExclusiveRange.to)

myExclusiveRange = 1>..<5
print(myExclusiveRange.from)
print(myExclusiveRange.to)