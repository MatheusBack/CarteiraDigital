using Microsoft.Extensions.Time.Testing;

using Wallet.Domain.Accounts;

namespace Wallet.Domain.Tests.Builders;

/// <summary>Relógio controlado nos testes. Nunca usamos DateTime.Now.</summary>
internal static class TestClock
{
    /// <summary>13/11/2026 12:00 no horário de Brasília (15:00 UTC).</summary>
    public static readonly DateTimeOffset Start = new(2026, 11, 13, 15, 0, 0, TimeSpan.Zero);

    public static FakeTimeProvider New() => new(Start);

    /// <summary>Cria um instante a partir de um horário local de Brasília.</summary>
    public static DateTimeOffset SaoPaulo(int year, int month, int day, int hour, int minute = 0, int second = 0)
    {
        var local = new DateTime(year, month, day, hour, minute, second, DateTimeKind.Unspecified);
        var offset = DailyLimitPolicy.SaoPaulo.GetUtcOffset(local);
        return new DateTimeOffset(local, offset);
    }
}
