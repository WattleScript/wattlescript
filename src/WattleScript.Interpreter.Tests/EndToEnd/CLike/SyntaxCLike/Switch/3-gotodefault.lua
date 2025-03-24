function s1(arg) {
    switch(arg) {
       case 1:
            print('a');
            goto default;
       case 2:
            print('b');
       default:
            print('c');
    }
}

s1(1);
s1(2);
s1(3);