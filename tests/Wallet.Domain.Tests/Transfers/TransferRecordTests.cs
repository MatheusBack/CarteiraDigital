using Wallet.Domain.Common;
using Wallet.Domain.Transfers;

namespace Wallet.Domain.Tests.Transfers;

public class TransferRecordTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 13, 15, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(TransferStatus.Completed)]
    [InlineData(TransferStatus.Denied)]
    [InlineData(TransferStatus.InReview)]
    public void Record_keeps_all_the_data(TransferStatus status)
    {
        var id = Guid.NewGuid();
        var from = Guid.NewGuid();
        var to = Guid.NewGuid();

        var transfer = Transfer.Record(id, from, to, Money.FromReais(10m), status, Now);

        transfer.Id.ShouldBe(id);
        transfer.FromAccountId.ShouldBe(from);
        transfer.ToAccountId.ShouldBe(to);
        transfer.Amount.ShouldBe(Money.FromReais(10m));
        transfer.Status.ShouldBe(status);
        transfer.CreatedAt.ShouldBe(Now);
    }

    [Fact]
    public void Same_origin_and_destination_is_a_programming_error()
    {
        var account = Guid.NewGuid();

        Should.Throw<DomainException>(() =>
            Transfer.Record(Guid.NewGuid(), account, account, Money.FromReais(1m), TransferStatus.Completed, Now));
    }

    [Fact]
    public void Zero_amount_is_a_programming_error() =>
        Should.Throw<DomainException>(() =>
            Transfer.Record(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Money.Zero, TransferStatus.Completed, Now));

    [Fact]
    public void Unknown_status_is_a_programming_error() =>
        Should.Throw<DomainException>(() =>
            Transfer.Record(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Money.FromReais(1m), (TransferStatus)99, Now));
}
