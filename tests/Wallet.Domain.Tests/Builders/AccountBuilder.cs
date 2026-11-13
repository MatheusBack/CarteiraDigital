using Wallet.Domain.Accounts;

namespace Wallet.Domain.Tests.Builders;

/// <summary>
/// Builder para montar contas nos testes de forma legível:
/// <c>AnAccount().WithBalance(reais: 100).Blocked().Build()</c>.
/// </summary>
internal sealed class AccountBuilder
{
    private static readonly string[] ValidCpfs =
    [
        "52998224725", "11144477735", "39053344705", "12345678909", "98765432100",
    ];

    private static int _nextCpf;

    private string _holderName = "Maria da Silva";
    private Cpf? _cpf;
    private decimal _balanceInReais;
    private bool _blocked;
    private DateTimeOffset _openedAt = TestClock.Start;

    public static AccountBuilder AnAccount() => new();

    public AccountBuilder Named(string holderName)
    {
        _holderName = holderName;
        return this;
    }

    public AccountBuilder WithCpf(string cpf)
    {
        _cpf = Cpf.Parse(cpf);
        return this;
    }

    public AccountBuilder WithBalance(decimal reais)
    {
        _balanceInReais = reais;
        return this;
    }

    public AccountBuilder Blocked()
    {
        _blocked = true;
        return this;
    }

    public AccountBuilder OpenedAt(DateTimeOffset openedAt)
    {
        _openedAt = openedAt;
        return this;
    }

    public Account Build()
    {
        var cpf = _cpf ?? Cpf.Parse(ValidCpfs[Interlocked.Increment(ref _nextCpf) % ValidCpfs.Length]);
        var account = Account.Open(_holderName, cpf, _openedAt);

        // saldo inicial via depósitos reais (respeitando o limite por depósito), para o ledger ficar consistente
        var remaining = Money.FromReais(_balanceInReais);
        while (!remaining.IsZero)
        {
            var chunk = remaining > Account.MaxDepositPerOperation ? Account.MaxDepositPerOperation : remaining;
            account.Deposit(chunk, _openedAt).IsSuccess.ShouldBeTrue();
            remaining -= chunk;
        }

        if (_blocked)
        {
            account.Block();
        }

        return account;
    }
}
