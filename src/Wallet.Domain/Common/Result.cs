namespace Wallet.Domain.Common;

/// <summary>
/// Resultado de uma operação de negócio: sucesso ou um <see cref="Common.Error"/>.
/// Regras de negócio violadas (saldo insuficiente, conta bloqueada...) retornam <see cref="Result"/>;
/// uso incorreto do código (valor negativo, argumento nulo) lança exceção.
/// </summary>
public sealed class Result
{
    private static readonly Result Success = new(null);

    private Result(Error? error) => Error = error;

    public Error? Error { get; }

    public bool IsSuccess => Error is null;

    public bool IsFailure => !IsSuccess;

    public static Result Ok() => Success;

    public static Result Fail(Error error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new Result(error);
    }

    public static implicit operator Result(Error error) => Fail(error);

    public static Result FromError(Error error) => Fail(error);

    public override string ToString() => IsSuccess ? "Ok" : $"Fail({Error!.Code})";
}
