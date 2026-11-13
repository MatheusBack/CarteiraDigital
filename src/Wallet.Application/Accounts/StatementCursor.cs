using System.Buffers.Text;
using System.Globalization;
using System.Text;

namespace Wallet.Application.Accounts;

/// <summary>
/// Cursor opaco do extrato: aponta para o último lançamento já entregue. A próxima página começa
/// no lançamento anterior a ele (paginação keyset: não usa OFFSET, então não fica lenta nem pula itens).
/// </summary>
public readonly record struct StatementCursor(long Before)
{
    private const string Prefix = "v1:";

    public string Encode() =>
        Base64Url.EncodeToString(Encoding.ASCII.GetBytes(Prefix + Before.ToString(CultureInfo.InvariantCulture)));

    public static bool TryParse(string? value, out StatementCursor cursor)
    {
        cursor = default;
        if (string.IsNullOrEmpty(value) || value.Length > 64)
        {
            return false;
        }

        try
        {
            var text = Encoding.ASCII.GetString(Base64Url.DecodeFromChars(value));
            if (text.StartsWith(Prefix, StringComparison.Ordinal)
                && long.TryParse(text.AsSpan(Prefix.Length), NumberStyles.None, CultureInfo.InvariantCulture, out var before)
                && before > 0)
            {
                cursor = new StatementCursor(before);
                return true;
            }
        }
        catch (FormatException)
        {
        }

        return false;
    }
}
