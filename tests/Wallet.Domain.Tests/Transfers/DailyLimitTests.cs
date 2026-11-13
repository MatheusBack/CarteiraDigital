using Wallet.Domain.Accounts;
using Wallet.Domain.Tests.Builders;
using Wallet.Domain.Transfers;

using static Wallet.Domain.Tests.Builders.AccountBuilder;

namespace Wallet.Domain.Tests.Transfers;

/// <summary>Limite diário de R$ 5.000,00 em transferências, contado no dia de Brasília.</summary>
public class DailyLimitTests
{
    private readonly DailyLimitPolicy _limit = DailyLimitPolicy.Default;

    [Theory]
    [InlineData(5_000.00, true)]   // exatamente no limite: permitido
    [InlineData(5_000.01, false)]  // 1 centavo acima: negado
    public void Single_transfer_at_the_limit_boundary(decimal reais, bool allowed)
    {
        var (from, to) = Accounts();

        var result = Transfer(from, to, reais, TestClock.Start);

        result.IsSuccess.ShouldBe(allowed);
        if (!allowed)
        {
            result.Error.ShouldBe(Errors.DailyLimitExceeded);
        }
    }

    [Fact]
    public void Limit_is_cumulative_within_the_same_day()
    {
        var (from, to) = Accounts();
        var clock = TestClock.New();

        Transfer(from, to, 3_000m, clock.GetUtcNow()).IsSuccess.ShouldBeTrue();
        clock.Advance(TimeSpan.FromHours(1));
        Transfer(from, to, 2_000m, clock.GetUtcNow()).IsSuccess.ShouldBeTrue();
        clock.Advance(TimeSpan.FromHours(1));

        Transfer(from, to, 0.01m, clock.GetUtcNow()).Error.ShouldBe(Errors.DailyLimitExceeded);
    }

    [Fact]
    public void Limit_resets_at_midnight_in_sao_paulo()
    {
        var (from, to) = Accounts();

        Transfer(from, to, 5_000m, TestClock.SaoPaulo(2026, 11, 13, 23, 59, 59)).IsSuccess.ShouldBeTrue();

        Transfer(from, to, 0.01m, TestClock.SaoPaulo(2026, 11, 13, 23, 59, 59)).IsSuccess.ShouldBeFalse();
        Transfer(from, to, 5_000m, TestClock.SaoPaulo(2026, 11, 14, 0, 0, 0)).IsSuccess.ShouldBeTrue();
    }

    /// <summary>
    /// 20h e 22h em Brasília são 23h e 01h (do dia seguinte) em UTC.
    /// Se o código usasse o dia em UTC, a segunda transferência passaria indevidamente.
    /// </summary>
    [Fact]
    public void Day_is_the_sao_paulo_calendar_day_not_the_utc_day()
    {
        var (from, to) = Accounts();
        var eightPm = TestClock.SaoPaulo(2026, 11, 13, 20, 0);
        var tenPm = TestClock.SaoPaulo(2026, 11, 13, 22, 0);
        tenPm.UtcDateTime.Day.ShouldBe(14); // sanidade: já é outro dia em UTC

        Transfer(from, to, 5_000m, eightPm).IsSuccess.ShouldBeTrue();

        Transfer(from, to, 1m, tenPm).Error.ShouldBe(Errors.DailyLimitExceeded);
    }

    [Fact]
    public void Withdrawals_and_received_transfers_do_not_count_toward_the_limit()
    {
        var (from, to) = Accounts();
        var now = TestClock.Start;

        from.Withdraw(Money.FromReais(1_000m), now).IsSuccess.ShouldBeTrue();
        Transfer(to: from, from: AnAccount().WithBalance(3_000m).Build(), reais: 3_000m, at: now)
            .IsSuccess.ShouldBeTrue();

        Transfer(from, to, 5_000m, now).IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public void Failed_transfer_does_not_consume_the_limit()
    {
        var (from, to) = Accounts(balance: 4_000m);
        var now = TestClock.Start;

        Transfer(from, to, 4_500m, now).Error.ShouldBe(Errors.InsufficientFunds);
        from.Deposit(Money.FromReais(10_000m), now);

        Transfer(from, to, 5_000m, now).IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public void Custom_policy_with_another_limit_is_respected()
    {
        var (from, to) = Accounts();
        var policy = new DailyLimitPolicy(Money.FromReais(100m), DailyLimitPolicy.SaoPaulo);

        TransferService.Execute(from, to, Money.FromReais(100.01m), Guid.NewGuid(), TestClock.Start, policy)
            .Error.ShouldBe(Errors.DailyLimitExceeded);
    }

    private static (Account From, Account To) Accounts(decimal balance = 20_000m) =>
        (AnAccount().WithBalance(balance).Build(), AnAccount().Build());

    private Common.Result Transfer(Account from, Account to, decimal reais, DateTimeOffset at) =>
        TransferService.Execute(from, to, Money.FromReais(reais), Guid.NewGuid(), at, _limit);
}
