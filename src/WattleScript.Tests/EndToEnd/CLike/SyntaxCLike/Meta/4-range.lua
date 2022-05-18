function prototype.range.toTable()
{
    local tbl = {}
    for(i in this) {
        table.insert(tbl, i);
    }
    return tbl;
}

print((1..4).toTable().length);
