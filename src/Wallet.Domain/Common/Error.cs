namespace Wallet.Domain.Common;

/// <summary>
/// Erro de regra de negócio. O <see cref="Code"/> é estável (pode ser usado pela API e pelos testes);
/// a <see cref="Message"/> é para humanos.
/// </summary>
public sealed record Error(string Code, string Message);
