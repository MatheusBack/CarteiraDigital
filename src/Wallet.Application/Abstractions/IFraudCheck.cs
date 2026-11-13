namespace Wallet.Application.Abstractions;

public enum FraudDecision
{
    Approved = 1,
    Denied = 2,

    /// <summary>O serviço não respondeu a tempo ou respondeu com erro.</summary>
    Unavailable = 3,
}

public sealed record FraudCheckRequest(Guid TransferId, Guid FromAccountId, Guid ToAccountId, long AmountCents);

/// <summary>Serviço antifraude externo.</summary>
public interface IFraudCheck
{
    /// <summary>Nunca lança exceção por falha do serviço: nesses casos retorna <see cref="FraudDecision.Unavailable"/>.</summary>
    Task<FraudDecision> CheckAsync(FraudCheckRequest request, CancellationToken cancellationToken);
}
