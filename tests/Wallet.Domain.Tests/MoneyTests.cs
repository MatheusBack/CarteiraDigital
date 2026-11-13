using Wallet.Domain.Common;

namespace Wallet.Domain.Tests;

public class MoneyTests
{
    [Theory]
    [InlineData(0, 0)]
    [InlineData(10.5, 1050)]
    [InlineData(0.01, 1)]
    [InlineData(1234.56, 123456)]
    public void FromReais_converts_to_cents(decimal reais, long expectedCents) =>
        Money.FromReais(reais).Cents.ShouldBe(expectedCents);

    [Fact]
    public void FromReais_rejects_more_than_two_decimal_places() =>
        Should.Throw<DomainException>(() => Money.FromReais(0.001m));

    [Fact]
    public void Negative_values_are_rejected()
    {
        Should.Throw<DomainException>(() => Money.FromCents(-1));
        Should.Throw<DomainException>(() => Money.FromReais(-0.01m));
    }

    [Fact]
    public void Addition_sums_cents() =>
        (Money.FromReais(10.25m) + Money.FromReais(0.75m)).ShouldBe(Money.FromReais(11m));

    [Fact]
    public void Subtraction_that_would_be_negative_throws() =>
        Should.Throw<DomainException>(() => Money.FromReais(1m) - Money.FromReais(1.01m));

    [Fact]
    public void Subtraction_down_to_exactly_zero_is_allowed() =>
        (Money.FromReais(5m) - Money.FromReais(5m)).IsZero.ShouldBeTrue();

    [Fact]
    public void Addition_overflow_is_detected_instead_of_wrapping_around() =>
        Should.Throw<OverflowException>(() => Money.FromCents(long.MaxValue) + Money.FromCents(1));

    [Fact]
    public void Comparison_operators_follow_cents()
    {
        var one = Money.FromCents(100);
        var two = Money.FromCents(200);

        (two > one).ShouldBeTrue();
        (one < two).ShouldBeTrue();
        (one >= Money.FromCents(100)).ShouldBeTrue();
        (one <= Money.FromCents(100)).ShouldBeTrue();
        one.CompareTo(two).ShouldBeLessThan(0);
    }

    [Theory]
    [InlineData(0, "R$ 0,00")]
    [InlineData(150, "R$ 1,50")]
    [InlineData(123456789, "R$ 1.234.567,89")]
    public void ToString_uses_brazilian_format(long cents, string expected) =>
        Money.FromCents(cents).ToString().ShouldBe(expected);

    /// <summary>Documenta por que dinheiro não usa double.</summary>
    [Fact]
    public void Money_is_exact_where_double_is_not()
    {
        (0.1 + 0.2).ShouldNotBe(0.3);

        (Money.FromReais(0.1m) + Money.FromReais(0.2m)).ShouldBe(Money.FromReais(0.3m));
    }
}
