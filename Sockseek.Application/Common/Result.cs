namespace Sockseek.Application.Common;

public class Result
{
    protected Result(bool isSuccess, Error error)
    {
        if (isSuccess && error != Error.None)
            throw new ArgumentException("Successful results cannot contain an error.", nameof(error));

        if (!isSuccess && error == Error.None)
            throw new ArgumentException("Failed results must contain an error.", nameof(error));

        IsSuccess = isSuccess;
        Error = error;
    }

    public bool IsSuccess { get; }

    public bool IsFailure => !IsSuccess;

    public Error Error { get; }

    public static Result Success() => new(true, Error.None);

    public static Result Failure(Error error) => new(false, error);
}

public sealed class Result<TValue> : Result
{
    private readonly TValue? value;

    private Result(TValue value)
        : base(true, Error.None)
    {
        this.value = value;
    }

    private Result(Error error)
        : base(false, error)
    {
        value = default;
    }

    public TValue Value => IsSuccess
        ? value!
        : throw new InvalidOperationException("A failed result does not have a value.");

    public static Result<TValue> Success(TValue value) => new(value);

    public static new Result<TValue> Failure(Error error) => new(error);
}
