using System.Diagnostics.CodeAnalysis;

namespace RestaurantAI.SharedKernel.Results;

public class Result
{
    protected Result(Error? error) => Error = error;

    public Error? Error { get; }

    [MemberNotNullWhen(false, nameof(Error))]
    public bool IsSuccess => Error is null;

    public static Result Success() => new(null);

    public static Result Failure(Error error) => new(error);

    public static Result<T> Success<T>(T value) => new(value, null);

    public static Result<T> Failure<T>(Error error) => new(default, error);
}

public sealed class Result<T> : Result
{
    private readonly T? _value;

    internal Result(T? value, Error? error)
        : base(error) => _value = value;

    public T Value => IsSuccess
        ? _value!
        : throw new InvalidOperationException($"Cannot access the value of a failed result ({Error.Code}).");

    public Result<TOut> Map<TOut>(Func<T, TOut> map) => IsSuccess ? Success(map(Value)) : Failure<TOut>(Error);

    public static implicit operator Result<T>(T value) => Success(value);

    public static implicit operator Result<T>(Error error) => Failure<T>(error);
}
