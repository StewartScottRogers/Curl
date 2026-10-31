namespace Curl.Extended.Protocol.AzureStorage.Bodies;

/// <summary>
/// The outcome of reading one Azure Storage response body: either the value it holds or, when the body
/// is not well-formed XML or JSON or lacks a value the body must have, the reason it could not be read.
/// </summary>
/// <typeparam name="T">The value the body holds.</typeparam>
public sealed class AzureStorageBodyParseResult<T>
    where T : class
{
    private AzureStorageBodyParseResult(T? value, string? failure)
    {
        Value = value;
        Failure = failure;
    }

    /// <summary>Gets the value the body holds, or <see langword="null"/> when it could not be read.</summary>
    public T? Value { get; }

    /// <summary>Gets why the body could not be read, or <see langword="null"/> when it was read.</summary>
    public string? Failure { get; }

    /// <summary>Gets a value indicating whether the body was read.</summary>
    public bool Succeeded => Value is not null;

    /// <summary>Returns the result of a body that was read.</summary>
    /// <param name="value">The value the body holds.</param>
    /// <returns>A succeeded result.</returns>
    public static AzureStorageBodyParseResult<T> Success(T value) => new(value, null);

    /// <summary>Returns the result of a body that could not be read.</summary>
    /// <param name="failure">Why the body could not be read.</param>
    /// <returns>A failed result.</returns>
    public static AzureStorageBodyParseResult<T> Malformed(string failure) => new(null, failure);
}
