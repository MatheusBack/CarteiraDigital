namespace Wallet.Domain.Common;

/// <summary>
/// Lançada quando um invariante do domínio seria violado por uso incorreto do código
/// (ex.: criar <see cref="Money"/> negativo). Não é usada para regras de negócio esperadas.
/// </summary>
public sealed class DomainException : Exception
{
    public DomainException()
    {
    }

    public DomainException(string message)
        : base(message)
    {
    }

    public DomainException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
