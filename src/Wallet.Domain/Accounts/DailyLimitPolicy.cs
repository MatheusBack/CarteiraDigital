namespace Wallet.Domain.Accounts;

/// <summary>
/// Limite diário de transferências enviadas por conta (RF04).
/// O "dia" é o dia civil no fuso configurado (padrão: America/Sao_Paulo), não o dia em UTC.
/// </summary>
public sealed class DailyLimitPolicy
{
    public DailyLimitPolicy(Money limit, TimeZoneInfo timeZone)
    {
        ArgumentNullException.ThrowIfNull(timeZone);
        Limit = limit;
        TimeZone = timeZone;
    }

    public static TimeZoneInfo SaoPaulo { get; } = TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo");

    /// <summary>R$ 5.000,00 por dia, no horário de Brasília.</summary>
    public static DailyLimitPolicy Default { get; } = new(Money.FromReais(5_000m), SaoPaulo);

    public Money Limit { get; }

    public TimeZoneInfo TimeZone { get; }

    public bool Allows(Account account, Money amount, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(account);

        var alreadySent = TransferredOutOn(account, LocalDay(now));
        return alreadySent.Cents + amount.Cents <= Limit.Cents;
    }

    /// <summary>Total já transferido pela conta no dia civil informado (no fuso da política).</summary>
    public Money TransferredOutOn(Account account, DateOnly day)
    {
        ArgumentNullException.ThrowIfNull(account);

        long cents = 0;
        foreach (var entry in account.Entries)
        {
            if (entry.Type == EntryType.TransferOut && LocalDay(entry.OccurredAt) == day)
            {
                cents += entry.Amount.Cents;
            }
        }

        return Money.FromCents(cents);
    }

    public DateOnly LocalDay(DateTimeOffset instant) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(instant, TimeZone).DateTime);
}
