Account = {balance = 100}
function Account:withdraw (v)
    this.balance = this.balance - v
end

Account::withdraw(60)
print(Account.balance)