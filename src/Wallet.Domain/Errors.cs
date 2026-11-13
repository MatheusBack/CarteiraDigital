using Wallet.Domain.Common;

namespace Wallet.Domain;

/// <summary>Catálogo dos erros de negócio do domínio. Os códigos são estáveis.</summary>
public static class Errors
{
    public static readonly Error InvalidAmount =
        new("invalid-amount", "O valor da operação deve ser maior que zero.");

    public static readonly Error InsufficientFunds =
        new("insufficient-funds", "Saldo insuficiente.");

    public static readonly Error AccountBlocked =
        new("account-blocked", "A conta está bloqueada.");

    public static readonly Error DestinationAccountBlocked =
        new("destination-account-blocked", "A conta de destino está bloqueada.");

    public static readonly Error SameAccount =
        new("same-account", "Não é possível transferir para a própria conta.");

    public static readonly Error DailyLimitExceeded =
        new("daily-limit-exceeded", "Limite diário de transferências excedido.");

    public static readonly Error DepositLimitExceeded =
        new("deposit-limit-exceeded", "O valor excede o limite por depósito.");
}
