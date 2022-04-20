function s1(arg) {
    switch(arg) {
        case 'd':
        case 'f':
        case 'e':
        {
            print(1);
        }
        case 'a':
        case 'b':
        case 'c': {
            print(2);
        }
        default:
            print('error');
    }
}
s1('a');
s1('b');
s1('c');
s1('d');
s1('e');
s1('f');
