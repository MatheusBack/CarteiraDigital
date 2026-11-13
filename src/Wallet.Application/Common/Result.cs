using System.Diagnostics.CodeAnalysis;

using Wallet.Domain.Common;

namespace Wallet.Application.Common;

/// <summary>Resultado de um caso de uso: um valor em caso de sucesso, ou um <see cref="Error"/>.</summary>
[SuppressMessage(
    "Design",
    "CA1000:Do not declare static members on generic types",
    Justification = "Fábricas Ok/Fail são o padrão de uso do tipo; as conversões implícitas cobrem o caso comum.")]
public sealed class Result<T>
{
    private readonly T? _value;

    private Result(T? value, Error? error)
    {
        _value = value;
        Error = error;
    }

    public Error? Error { get; }

    public bool IsSuccess => Error is null;

    public bool IsFailure => !IsSuccess;

    /// <summary>O valor do sucesso. Lança exceção se o resultado for uma falha.</summary>
    public T Value => IsSuccess
        ? _value!
        : throw new InvalidOperationException($"Resultado com falha ({Error!.Code}) não tem valor.");

    public static Result<T> Ok(T value) => new(value, null);

    public static Result<T> Fail(Error error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new Result<T>(default, error);
    }

    public static implicit operator Result<T>(T value) => Ok(value);

    public static implicit operator Result<T>(Error error) => Fail(error);

    public override string ToString() => IsSuccess ? $"Ok({_value})" : $"Fail({Error!.Code})";
}
