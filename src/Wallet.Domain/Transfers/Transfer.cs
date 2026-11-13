using Wallet.Domain.Common;

namespace Wallet.Domain.Transfers;

/// <summary>Situação de uma transferência.</summary>
public enum TransferStatus
{
    /// <summary>Dinheiro movimentado entre as contas.</summary>
    Completed = 1,

    /// <summary>Recusada pela análise antifraude. Nada foi movimentado.</summary>
    Denied = 2,

    /// <summary>Antifraude não respondeu a tempo. Nada foi movimentado; aguarda análise.</summary>
    InReview = 3,
}

/// <summary>
/// Registro de uma transferência. A movimentação de dinheiro em si é feita por
/// <see cref="TransferService"/>; esta classe guarda o histórico e a situação.
/// </summary>
public sealed class Transfer
{
    private Transfer(
        Guid id,
        Guid fromAccountId,
        Guid toAccountId,
        Money amount,
        TransferStatus status,
        DateTimeOffset createdAt)
    {
        Id = id;
        FromAccountId = fromAccountId;
        ToAccountId = toAccountId;
        Amount = amount;
        Status = status;
        CreatedAt = createdAt;
    }

    public Guid Id { get; }

    public Guid FromAccountId { get; }

    public Guid ToAccountId { get; }

    public Money Amount { get; }

    public TransferStatus Status { get; private set; }

    public DateTimeOffset CreatedAt { get; }

    public static Transfer Record(
        Guid id,
        Guid fromAccountId,
        Guid toAccountId,
        Money amount,
        TransferStatus status,
        DateTimeOffset createdAt)
    {
        if (fromAccountId == toAccountId)
        {
            throw new DomainException("Origem e destino de uma transferência devem ser diferentes.");
        }

        if (amount.IsZero)
        {
            throw new DomainException("O valor de uma transferência deve ser maior que zero.");
        }

        if (!Enum.IsDefined(status))
        {
            throw new DomainException($"Situação de transferência inválida: {status}.");
        }

        return new Transfer(id, fromAccountId, toAccountId, amount, status, createdAt);
    }
}
