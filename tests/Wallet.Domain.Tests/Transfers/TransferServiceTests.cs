using Wallet.Domain.Accounts;
using Wallet.Domain.Tests.Builders;
using Wallet.Domain.Transfers;

using static Wallet.Domain.Tests.Builders.AccountBuilder;

namespace Wallet.Domain.Tests.Transfers;

public class TransferServiceTests
{
    private readonly DateTimeOffset _now = TestClock.Start;
    private readonly DailyLimitPolicy _limit = DailyLimitPolicy.Default;

    [Fact]
    public void Successful_transfer_moves_money_and_records_both_sides_with_the_same_id()
    {
        var from = AnAccount().WithBalance(1_000m).Build();
        var to = AnAccount().Build();
        var transferId = Guid.NewGuid();

        var result = TransferService.Execute(from, to, Money.FromReais(250m), transferId, _now, _limit);

        result.IsSuccess.ShouldBeTrue();
        from.Balance.ShouldBe(Money.FromReais(750m));
        to.Balance.ShouldBe(Money.FromReais(250m));

        from.Entries[^1].ShouldBe(new LedgerEntry(from.Id, EntryType.TransferOut, Money.FromReais(250m), _now, transferId));
        to.Entries[^1].ShouldBe(new LedgerEntry(to.Id, EntryType.TransferIn, Money.FromReais(250m), _now, transferId));
    }

    [Fact]
    public void Transfer_with_insufficient_funds_changes_nothing()
    {
        var from = AnAccount().WithBalance(100m).Build();
        var to = AnAccount().Build();

        var result = TransferService.Execute(from, to, Money.FromReais(100.01m), Guid.NewGuid(), _now, _limit);

        result.Error.ShouldBe(Errors.InsufficientFunds);
        AssertUnchanged(from, Money.FromReais(100m), entries: 1);
        AssertUnchanged(to, Money.Zero, entries: 0);
    }

    [Fact]
    public void Transfer_of_the_entire_balance_is_allowed()
    {
        var from = AnAccount().WithBalance(100m).Build();
        var to = AnAccount().Build();

        TransferService.Execute(from, to, Money.FromReais(100m), Guid.NewGuid(), _now, _limit)
            .IsSuccess.ShouldBeTrue();

        from.Balance.ShouldBe(Money.Zero);
    }

    [Fact]
    public void Transfer_to_the_same_account_is_rejected()
    {
        var account = AnAccount().WithBalance(100m).Build();

        TransferService.Execute(account, account, Money.FromReais(10m), Guid.NewGuid(), _now, _limit)
            .Error.ShouldBe(Errors.SameAccount);

        AssertUnchanged(account, Money.FromReais(100m), entries: 1);
    }

    [Fact]
    public void Transfer_of_zero_is_rejected()
    {
        var from = AnAccount().WithBalance(100m).Build();
        var to = AnAccount().Build();

        TransferService.Execute(from, to, Money.Zero, Guid.NewGuid(), _now, _limit)
            .Error.ShouldBe(Errors.InvalidAmount);
    }

    [Fact]
    public void Blocked_origin_cannot_send()
    {
        var from = AnAccount().WithBalance(100m).Blocked().Build();
        var to = AnAccount().Build();

        TransferService.Execute(from, to, Money.FromReais(10m), Guid.NewGuid(), _now, _limit)
            .Error.ShouldBe(Errors.AccountBlocked);

        AssertUnchanged(to, Money.Zero, entries: 0);
    }

    /// <summary>
    /// Se o destino estiver bloqueado, a origem NÃO pode ser debitada.
    /// Esse teste protege a ordem das verificações no TransferService.
    /// </summary>
    [Fact]
    public void Blocked_destination_is_rejected_before_debiting_the_origin()
    {
        var from = AnAccount().WithBalance(100m).Build();
        var to = AnAccount().Blocked().Build();

        TransferService.Execute(from, to, Money.FromReais(10m), Guid.NewGuid(), _now, _limit)
            .Error.ShouldBe(Errors.DestinationAccountBlocked);

        AssertUnchanged(from, Money.FromReais(100m), entries: 1);
    }

    [Fact]
    public void Money_is_conserved_across_a_sequence_of_transfers()
    {
        var a = AnAccount().WithBalance(1_000m).Build();
        var b = AnAccount().WithBalance(500m).Build();
        var c = AnAccount().Build();
        var totalBefore = a.Balance + b.Balance + c.Balance;

        TransferService.Execute(a, b, Money.FromReais(300m), Guid.NewGuid(), _now, _limit);
        TransferService.Execute(b, c, Money.FromReais(700m), Guid.NewGuid(), _now, _limit);
        TransferService.Execute(c, a, Money.FromReais(50.25m), Guid.NewGuid(), _now, _limit);
        TransferService.Execute(c, b, Money.FromReais(10_000m), Guid.NewGuid(), _now, _limit); // falha

        (a.Balance + b.Balance + c.Balance).ShouldBe(totalBefore);
        foreach (var account in new[] { a, b, c })
        {
            account.RecomputeBalanceFromLedger().ShouldBe(account.Balance);
        }
    }

    private static void AssertUnchanged(Account account, Money balance, int entries)
    {
        account.Balance.ShouldBe(balance);
        account.Entries.Count.ShouldBe(entries);
    }
}
