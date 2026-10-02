using System;

namespace TurboTurbo;

/// <summary>
/// The outcome of an operation that yields <typeparamref name="T"/> on success or an <see cref="Error"/> on failure.
/// </summary>
internal readonly struct Result<T>
{
    private readonly T _value;

    private Result(T value, Error? error)
    {
        _value = value;
        Error = error;
    }

    /// <summary>The failure, or null on success.</summary>
    public Error? Error { get; }

    public bool IsSuccess => Error is null;

    /// <summary>The success value. Throws when the result is a failure.</summary>
    public T Value => IsSuccess ? _value : throw new InvalidOperationException("a failed result has no value");
    
    public static implicit operator Result<T>(T value) => Ok(value);

    public static implicit operator Result<T>(Error error) => Fail(error);

    public static Result<T> Ok(T value)
    {
        return new Result<T>(value, null);
    }

    public static Result<T> Fail(Error error) => new(default, error);

    /// <summary>Runs exactly one of the two branches.</summary>
    public void Switch(Action<T> onSuccess, Action<Error> onFailure)
    {
        if (this.Error is { } error) onFailure(error);
        else onSuccess(_value);
    }
}

internal static class ResultExtensions
{
    public static Result<T2> Select<T1, T2>(this Result<T1> source, Func<T1, T2> map)
    {
        return source.IsSuccess ? Result<T2>.Ok(map(source.Value)) : Result<T2>.Fail(source.Error!.Value);
    }
    
    public static Result<T2> SelectMany<T1, T2>(this Result<T1> source, Func<T1, Result<T2>> map)
    {
        return source.IsSuccess ? map(source.Value) : Result<T2>.Fail(source.Error!.Value);
    }
    
    /// <summary>Wraps a value and an optional error into a <see cref="Result{T}"/>.</summary>
    public static Result<T> ToResult<T>(this T value, Error? error) =>
        error is null ? Result<T>.Ok(value) : Result<T>.Fail(error.Value);
}
