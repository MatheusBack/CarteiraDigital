using System.Globalization;

using Wallet.Domain.Common;

namespace Wallet.Domain;

/// <summary>
/// Quantia em reais, armazenada em centavos (<see cref="long"/>). Nunca é negativa.
/// Dinheiro nunca usa <see cref="double"/>: 0.1 + 0.2 != 0.3 em ponto flutuante.
/// </summary>
public readonly record struct Money : IComparable<Money>
{
    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");

    private Money(long cents) => Cents = cents;

    public static Money Zero { get; } = new(0);

    public long Cents { get; }

    public bool IsZero => Cents == 0;

    /// <summary>Valor em reais (ex.: 1234.56). Útil para exibição e contratos de API.</summary>
    public decimal Reais => Cents / 100m;

    public static Money FromCents(long cents) =>
        cents < 0
            ? throw new DomainException($"Valor negativo não é permitido: {cents} centavos.")
            : new Money(cents);

    public static Money FromReais(decimal reais)
    {
        if (reais < 0)
        {
            throw new DomainException($"Valor negativo não é permitido: {reais}.");
        }

        var cents = reais * 100m;
        if (cents != decimal.Truncate(cents))
        {
            throw new DomainException($"O valor deve ter no máximo 2 casas decimais: {reais}.");
        }

        return new Money(checked((long)cents));
    }

    public static Money operator +(Money left, Money right) => left.Add(right);

    public static Money operator -(Money left, Money right) => left.Subtract(right);

    public static bool operator >(Money left, Money right) => left.Cents > right.Cents;

    public static bool operator <(Money left, Money right) => left.Cents < right.Cents;

    public static bool operator >=(Money left, Money right) => left.Cents >= right.Cents;

    public static bool operator <=(Money left, Money right) => left.Cents <= right.Cents;

    public Money Add(Money other) => new(checked(Cents + other.Cents));

    public Money Subtract(Money other) =>
        other.Cents > Cents
            ? throw new DomainException("O resultado da subtração seria negativo.")
            : new Money(Cents - other.Cents);

    public int CompareTo(Money other) => Cents.CompareTo(other.Cents);

    /// <summary>Formato brasileiro, ex.: "R$ 1.234,56".</summary>
    public override string ToString() =>
        "R$ " + Reais.ToString("N2", PtBr);
}
