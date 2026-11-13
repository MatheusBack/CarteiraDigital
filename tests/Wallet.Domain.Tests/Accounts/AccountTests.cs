using Wallet.Domain.Accounts;
using Wallet.Domain.Common;
using Wallet.Domain.Tests.Builders;

using static Wallet.Domain.Tests.Builders.AccountBuilder;

namespace Wallet.Domain.Tests.Accounts;

public class AccountTests
{
    private readonly DateTimeOffset _now = TestClock.Start;

    // ---------- Abertura ----------

    [Fact]
    public void New_account_is_active_with_zero_balance_and_no_entries()
    {
        var account = Account.Open("  Maria da Silva ", Cpf.Parse("52998224725"), _now);

        account.HolderName.ShouldBe("Maria da Silva");
        account.Status.ShouldBe(AccountStatus.Active);
        account.Balance.ShouldBe(Money.Zero);
        account.Entries.ShouldBeEmpty();
        account.OpenedAt.ShouldBe(_now);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Holder_name_is_required(string name) =>
        Should.Throw<DomainException>(() => Account.Open(name, Cpf.Parse("52998224725"), _now));

    [Fact]
    public void Holder_name_has_maximum_length()
    {
        var cpf = Cpf.Parse("52998224725");

        Should.NotThrow(() => Account.Open(new string('a', Account.MaxHolderNameLength), cpf, _now));
        Should.Throw<DomainException>(() => Account.Open(new string('a', Account.MaxHolderNameLength + 1), cpf, _now));
    }

    // ---------- Depósito ----------

    [Fact]
    public void Deposit_increases_balance_and_records_a_credit_entry()
    {
        var account = AnAccount().Build();

        var result = account.Deposit(Money.FromReais(100m), _now);

        result.IsSuccess.ShouldBeTrue();
        account.Balance.ShouldBe(Money.FromReais(100m));
        var entry = account.Entries.ShouldHaveSingleItem();
        entry.Type.ShouldBe(EntryType.Deposit);
        entry.Amount.ShouldBe(Money.FromReais(100m));
        entry.IsCredit.ShouldBeTrue();
    }

    [Fact]
    public void Deposit_of_zero_is_rejected()
    {
        var account = AnAccount().Build();

        account.Deposit(Money.Zero, _now).Error.ShouldBe(Errors.InvalidAmount);
        account.Entries.ShouldBeEmpty();
    }

    [Fact]
    public void Deposit_exactly_at_the_per_operation_limit_is_accepted() =>
        AnAccount().Build().Deposit(Account.MaxDepositPerOperation, _now).IsSuccess.ShouldBeTrue();

    [Fact]
    public void Deposit_one_cent_above_the_per_operation_limit_is_rejected()
    {
        var account = AnAccount().Build();

        var result = account.Deposit(Account.MaxDepositPerOperation + Money.FromCents(1), _now);

        result.Error.ShouldBe(Errors.DepositLimitExceeded);
        account.Balance.ShouldBe(Money.Zero);
    }

    [Fact]
    public void Blocked_account_cannot_receive_deposits()
    {
        var account = AnAccount().Blocked().Build();

        account.Deposit(Money.FromReais(10m), _now).Error.ShouldBe(Errors.AccountBlocked);
    }

    // ---------- Saque ----------

    [Fact]
    public void Withdraw_decreases_balance_and_records_a_debit_entry()
    {
        var account = AnAccount().WithBalance(100m).Build();

        var result = account.Withdraw(Money.FromReais(30m), _now);

        result.IsSuccess.ShouldBeTrue();
        account.Balance.ShouldBe(Money.FromReais(70m));
        account.Entries[^1].Type.ShouldBe(EntryType.Withdrawal);
        account.Entries[^1].IsDebit.ShouldBeTrue();
    }

    [Fact]
    public void Withdraw_of_the_entire_balance_is_allowed()
    {
        var account = AnAccount().WithBalance(100m).Build();

        account.Withdraw(Money.FromReais(100m), _now).IsSuccess.ShouldBeTrue();
        account.Balance.ShouldBe(Money.Zero);
    }

    [Fact]
    public void Withdraw_one_cent_above_the_balance_fails_and_keeps_balance()
    {
        var account = AnAccount().WithBalance(100m).Build();
        var entriesBefore = account.Entries.Count;

        var result = account.Withdraw(Money.FromReais(100.01m), _now);

        result.Error.ShouldBe(Errors.InsufficientFunds);
        account.Balance.ShouldBe(Money.FromReais(100m));
        account.Entries.Count.ShouldBe(entriesBefore);
    }

    [Fact]
    public void Withdraw_of_zero_is_rejected() =>
        AnAccount().WithBalance(10m).Build().Withdraw(Money.Zero, _now).Error.ShouldBe(Errors.InvalidAmount);

    [Fact]
    public void Blocked_account_cannot_withdraw()
    {
        var account = AnAccount().WithBalance(100m).Blocked().Build();

        account.Withdraw(Money.FromReais(10m), _now).Error.ShouldBe(Errors.AccountBlocked);
        account.Balance.ShouldBe(Money.FromReais(100m));
    }

    [Fact]
    public void Unblocked_account_can_operate_again()
    {
        var account = AnAccount().WithBalance(100m).Blocked().Build();

        account.Unblock();

        account.Withdraw(Money.FromReais(10m), _now).IsSuccess.ShouldBeTrue();
    }

    // ---------- Ledger ----------

    [Fact]
    public void Balance_always_matches_the_ledger()
    {
        var account = AnAccount().Build();

        account.Deposit(Money.FromReais(500m), _now);
        account.Withdraw(Money.FromReais(120.50m), _now);
        account.Withdraw(Money.FromReais(1_000m), _now);   // falha: não deve gerar lançamento
        account.Deposit(Money.FromReais(0.50m), _now);

        account.Balance.ShouldBe(Money.FromReais(380m));
        account.RecomputeBalanceFromLedger().ShouldBe(account.Balance);
        account.Entries.Count.ShouldBe(3);
    }
}
