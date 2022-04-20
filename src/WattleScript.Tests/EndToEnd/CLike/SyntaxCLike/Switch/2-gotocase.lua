function s1(arg) {
    switch(arg) {
        case 'b':
            print('b');
        case 'a':
            print('a');
            goto case 'b';
    }
}

s1('a');
s1('b');