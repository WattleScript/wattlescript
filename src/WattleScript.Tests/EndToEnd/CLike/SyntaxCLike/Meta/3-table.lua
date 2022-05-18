function prototype.table.hello()
{
    return "hello";
}

tbl = {}
print(tbl.hello());
//special case for tables, we only index the prototype for direct method calls
//this should be nil
print(tbl.hello)