using System.Diagnostics.CodeAnalysis;

using Wallet.Domain.Common;

namespace Wallet.Domain.Accounts;

/// <summary>CPF válido (dígitos verificadores conferidos), guardado só com os 11 dígitos.</summary>
public sealed record Cpf
{
    private Cpf(string digits) => Digits = digits;

    /// <summary>Os 11 dígitos, sem pontuação.</summary>
    public string Digits { get; }

    /// <summary>Formato de exibição: 000.000.000-00.</summary>
    public string Formatted => $"{Digits[..3]}.{Digits[3..6]}.{Digits[6..9]}-{Digits[9..]}";

    public static Cpf Parse(string input) =>
        TryParse(input, out var cpf)
            ? cpf
            : throw new DomainException("CPF inválido.");

    public static bool TryParse(string? input, [NotNullWhen(true)] out Cpf? cpf)
    {
        cpf = null;
        if (string.IsNullOrWhiteSpace(input))
        {
            return false;
        }

        // aceita "529.982.247-25" ou "52998224725"; qualquer outro caractere invalida
        Span<int> digits = stackalloc int[11];
        var count = 0;
        foreach (var c in input)
        {
            if (char.IsAsciiDigit(c))
            {
                if (count == 11)
                {
                    return false;
                }

                digits[count++] = c - '0';
            }
            else if (c is not ('.' or '-' or ' '))
            {
                return false;
            }
        }

        if (count != 11 || AllSame(digits))
        {
            return false;
        }

        if (CheckDigit(digits[..9], firstWeight: 10) != digits[9] ||
            CheckDigit(digits[..10], firstWeight: 11) != digits[10])
        {
            return false;
        }

        cpf = new Cpf(string.Concat(digits.ToArray()));
        return true;
    }

    public override string ToString() => Formatted;

    private static int CheckDigit(ReadOnlySpan<int> digits, int firstWeight)
    {
        var sum = 0;
        for (var i = 0; i < digits.Length; i++)
        {
            sum += digits[i] * (firstWeight - i);
        }

        var rest = sum % 11;
        return rest < 2 ? 0 : 11 - rest;
    }

    private static bool AllSame(ReadOnlySpan<int> digits)
    {
        foreach (var d in digits)
        {
            if (d != digits[0])
            {
                return false;
            }
        }

        return true;
    }
}
