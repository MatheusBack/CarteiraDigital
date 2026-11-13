using Wallet.Domain.Transfers;

namespace Wallet.Application.Abstractions;

public interface ITransferRepository
{
    void Add(Transfer transfer);
}
