using Wallet.Domain.Accounts;
using Wallet.Domain.Common;

namespace Wallet.Domain.Tests.Accounts;

public class CpfTests
{
    [Theory]
    [InlineData("529.982.247-25")]
    [InlineData("52998224725")]
    [InlineData("111.444.777-35")]
    [InlineData(" 390.533.447-05 ")]
    public void Valid_cpfs_are_accepted(string input)
    {
        Cpf.TryParse(input, out var cpf).ShouldBeTrue();
        cpf!.Digits.Length.ShouldBe(11);
    }

    [Theory]
    [InlineData("529.982.247-24")]   // primeiro dígito ok, segundo errado
    [InlineData("529.982.247-15")]   // primeiro dígito errado
    [InlineData("111.111.111-11")]   // todos iguais passam no cálculo, mas são inválidos
    [InlineData("000.000.000-00")]
    [InlineData("5299822472")]       // 10 dígitos
    [InlineData("529982247250")]     // 12 dígitos
    [InlineData("529.982.247-2A")]   // letra
    [InlineData("529/982/247-25")]   // separador estranho
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Invalid_cpfs_are_rejected(string? input) =>
        Cpf.TryParse(input, out _).ShouldBeFalse();

    [Fact]
    public void Formatted_shows_punctuation() =>
        Cpf.Parse("52998224725").Formatted.ShouldBe("529.982.247-25");

    [Fact]
    public void Same_digits_are_equal_regardless_of_input_format() =>
        Cpf.Parse("529.982.247-25").ShouldBe(Cpf.Parse("52998224725"));

    [Fact]
    public void Parse_throws_for_invalid_cpf() =>
        Should.Throw<DomainException>(() => Cpf.Parse("123"));
}
