namespace FleetDelivery.BuildingBlocks.Results;

/// <summary>
/// A <see cref="Result"/> that carries a value on success.
/// </summary>
/// <typeparam name="TValue">The type of the success value.</typeparam>
public sealed class Result<TValue> : Result
{
    private readonly TValue? _value;

    internal Result(TValue? value, bool isSuccess, Error error)
        : base(isSuccess, error)
    {
        _value = value;
    }

    /// <summary>The success value. Throws if accessed on a failed result.</summary>
    public TValue Value => IsSuccess
        ? _value!
        : throw new InvalidOperationException("The value of a failed result cannot be accessed.");

    public static implicit operator Result<TValue>(TValue value) => Success(value);
}
