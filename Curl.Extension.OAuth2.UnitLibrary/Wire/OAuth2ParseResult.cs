namespace Curl.Extension.OAuth2.Wire;

/// <summary>
/// The outcome of reading one OAuth 2.0 JSON response body: either the value it holds or, when the
/// body is not a JSON object or lacks a member the response must have, the reason it could not be read.
/// </summary>
/// <typeparam name="T">The value the body holds.</typeparam>
public sealed class OAuth2ParseResult<T>
    where T : class
{
    private OAuth2ParseResult(T? value, string? failure)
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
    public static OAuth2ParseResult<T> Success(T value) => new(value, null);

    /// <summary>Returns the result of a body that could not be read.</summary>
    /// <param name="failure">Why the body could not be read.</param>
    /// <returns>A failed result.</returns>
    public static OAuth2ParseResult<T> Malformed(string failure) => new(null, failure);
}
