function s1(a) {
    switch(a) {
        case 5:
            i = 0
            while true {
                i++
                if i > 3 {
                    break;
                }
            }
            i = 7
            break;
         default:
            i = 99
    }
}

s1(5);
print(i);
s1(0);
print(i);